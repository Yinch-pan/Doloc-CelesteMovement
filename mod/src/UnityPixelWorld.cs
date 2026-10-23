using System;
using Doloc.CelesteMovement.Core;
using DolocTown;
using UnityEngine;

namespace Yinch.CelesteMovement;

internal sealed class UnityPixelWorld : IPixelWorld
{
    private readonly BodyController body;
    private Collider2D[] hits = new Collider2D[64];
    private readonly float scale;
    internal bool IgnorePlatforms;
    private Collider2D support;
    private Vector2 supportPosition;
    internal Vector2 Transport(PixelPlayer player,float dt,out Vector2 lift)
    {
        lift=Vector2.zero;
        Vector2 delta=Vector2.zero;
        if(support!=null&&(player.Grounded||player.State==PlayerMotion.Climb))
        {
            Vector2 now=support.transform.position;
            delta=now-supportPosition;
            lift=dt>0?delta/dt:Vector2.zero;
            player.Carry(delta.x/scale,delta.y/scale);
        }
        support=null;
        bool climbing=player.State==PlayerMotion.Climb;
        if(player.Grounded||climbing)
        {
            int count=Query(player.X-4+(climbing?player.Facing:0),player.Y-(climbing?0:1),8,player.Height);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(!Valid(hit)||hit.isTrigger) continue;
                if(Platform(hit)&&(climbing||IgnorePlatforms||player.Y*scale<hit.bounds.max.y-.001f)) continue;
                support=hit; supportPosition=hit.transform.position;
                if(lift==Vector2.zero&&hit.attachedRigidbody!=null) lift=hit.attachedRigidbody.velocity;
                break;
            }
        }
        return delta;
    }
    internal UnityPixelWorld(BodyController body, float scale) { this.body=body; this.scale=scale; }
    private int Query(float left,float bottom,float width,float height)
    {
        var center=new Vector2((left+width*.5f)*scale,(bottom+height*.5f)*scale);
        var size=new Vector2(width*scale-.0002f,height*scale-.0002f);
        int count=Physics2D.OverlapBoxNonAlloc(center,size,0,hits,DolocAPI.gameConfig.walkableMask);
        if(count==hits.Length)
        {
            if(hits.Length>=4096) throw new InvalidOperationException("Collision query saturated; stopping movement instead of ignoring terrain.");
            hits=new Collider2D[hits.Length*2];
            return Query(left,bottom,width,height);
        }
        return count;
    }
    private bool Valid(Collider2D hit) => hit!=null&&hit.enabled&&hit.attachedRigidbody!=body.BodyCollider.attachedRigidbody&&
        !Physics2D.GetIgnoreLayerCollision(body.gameObject.layer,hit.gameObject.layer);
    private static bool Platform(Collider2D hit) => hit.usedByEffector || hit.GetComponent(typeof(PlatformEntityOneway))!=null;
    public bool Solid(float left,float bottom,float width,float height)
    {
        int count=Query(left,bottom,width,height);
        for(int i=0;i<count;i++)
            if(Valid(hits[i])&&!hits[i].isTrigger&&!Platform(hits[i])) return true;
        return false;
    }
    public bool OneWay(float left,float bottom,float width,float height,float previousBottom)
    {
        if(IgnorePlatforms) return false;
        int count=Query(left,bottom,width,height);
        for(int i=0;i<count;i++)
            if(Valid(hits[i])&&Platform(hits[i])&&previousBottom*scale>=hits[i].bounds.max.y-.001f)
                return true;
        return false;
    }
    public bool OverlapsOneWay(float left,float bottom,float width,float height)
    {
        if(IgnorePlatforms) return false;
        int count=Query(left,bottom,width,height);
        for(int i=0;i<count;i++) if(Valid(hits[i])&&Platform(hits[i])) return true;
        return false;
    }
    public float? DashPlatformTop(float left,float bottom,float width,float height)
    {
        if(IgnorePlatforms) return null;
        int count=Query(left,bottom,width,height);
        float? result=null;
        for(int i=0;i<count;i++)
            if(Valid(hits[i])&&Platform(hits[i]))
            {
                float top=hits[i].bounds.max.y/scale;
                if(top>bottom && top-bottom<=6 && (!result.HasValue||top<result.Value)) result=top;
            }
        return result;
    }
}
