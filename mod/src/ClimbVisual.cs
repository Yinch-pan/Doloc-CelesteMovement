using System;
using System.Collections.Generic;
using DolocTown;
using UnityEngine;

namespace Yinch.CelesteMovement;

// Runtime sprite rig: retains original texture/atlas UVs and outfit materials.
// No source asset, animation controller or physics transform is modified.
public sealed class ClimbVisual : MonoBehaviour
{
    private BodyController body;
    private SpriteRenderer basis;
    private AgentHatRenderer hat;
    private readonly List<Part> parts=new List<Part>();
    private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    private sealed class Part
    {
        internal SpriteRenderer Source;
        internal MeshRenderer Display;
        internal Mesh Mesh;
        internal Sprite Sprite;
        internal Vector3[] Original, Deformed;
        internal Color[] Colors;
        internal bool Head, Hidden;
    }
    internal void Bind(BodyController value) { body=value; enabled=true; }
    private void Add(SpriteRenderer source,bool head)
    {
        if(source==null||parts.Exists(p=>p.Source==source)) return;
        var go=new GameObject("CelesteClimbRig"); go.layer=source.gameObject.layer;
        go.transform.SetParent(source.transform,false);
        var filter=(MeshFilter)go.AddComponent(typeof(MeshFilter));
        var renderer=(MeshRenderer)go.AddComponent(typeof(MeshRenderer));
        var mesh=new Mesh{name="CelesteClimbMesh"}; mesh.MarkDynamic();
        filter.sharedMesh=mesh;
        parts.Add(new Part{Source=source,Display=renderer,Mesh=mesh,Head=head,Hidden=source.forceRenderingOff});
    }
    private void Collect()
    {
        basis=(SpriteRenderer)body.GetComponent(typeof(SpriteRenderer));
        if(basis==null&&body.SplitRenderer!=null)
            basis=(SpriteRenderer)body.SplitRenderer.transform.parent.GetComponent(typeof(SpriteRenderer));
        Add(basis,false);
        if(body.SplitRenderer!=null)
        {
            Add(body.SplitRenderer.BodyRenderer,false);
            Add(body.SplitRenderer.HairRenderer,true);
        }
        hat=body.HatRenderer;
        if(hat!=null)
            foreach(var component in hat.GetComponentsInChildren(typeof(SpriteRenderer),true))
                Add((SpriteRenderer)component,true);
        if(MovementRuntime.Config.DiagnosticLogging)
            MovementRuntime.Monitor.Log("Climb rig active; native sprite layers="+parts.Count);
    }
    private void LateUpdate()
    {
        try
        {
            var state=MovementRuntime.Current(body);
            if(state?.Presentation==null||(!state.Presentation.Climbing&&!state.Presentation.CrouchPose)) { Restore(); return; }
            if(hat!=body.HatRenderer) Restore();
            if(parts.Count==0) Collect();
            if(basis?.sprite==null) return;
            var bounds=basis.sprite.bounds;
            float pixel=1/basis.sprite.pixelsPerUnit;
            foreach(var part in parts)
            {
                var source=part.Source;
                if(source==null||part.Display==null) continue;
                // MeshRenderer cannot honor SpriteMask interactions. Preserve those renderers.
                bool visible=source.enabled&&source.gameObject.activeInHierarchy&&!part.Hidden&&source.sprite!=null&&
                    source.maskInteraction==SpriteMaskInteraction.None;
                part.Display.enabled=visible;
                source.forceRenderingOff=visible||part.Hidden;
                if(!visible) continue;
                if(part.Sprite!=source.sprite) Build(part,source.sprite);
                var motion=state.Presentation;
                for(int i=0;i<part.Original.Length;i++)
                {
                    var local=part.Original[i];
                    if(source.flipX) local.x=-local.x;
                    if(source.flipY) local.y=-local.y;
                    var point=basis.transform.InverseTransformPoint(source.transform.TransformPoint(local));
                    motion.Offset((point.x-bounds.min.x)/bounds.size.x,(point.y-bounds.min.y)/bounds.size.y,
                        part.Head,out float dx,out float dy);
                    var offset=source.transform.InverseTransformVector(basis.transform.TransformVector(new Vector3(dx*pixel,dy*pixel,0)));
                    part.Deformed[i]=local+offset; part.Colors[i]=source.color;
                }
                part.Mesh.vertices=part.Deformed; part.Mesh.colors=part.Colors;
                PreserveSortPoint(part);
                part.Display.sharedMaterial=source.sharedMaterial;
                part.Display.sortingLayerID=source.sortingLayerID; part.Display.sortingOrder=source.sortingOrder;
                source.GetPropertyBlock(properties);
                properties.SetTexture("_MainTex",source.sprite.texture);
                properties.SetColor("_RendererColor",Color.white);
                properties.SetVector("_Flip",new Vector4(1,1,1,1));
                part.Display.SetPropertyBlock(properties);
            }
        }
        catch(Exception error)
        {
            Restore(); enabled=false;
            MovementRuntime.Monitor?.Log("Climb animation error: "+error,DTMAPI.Abstractions.LogLevel.Error);
        }
    }
    private static void PreserveSortPoint(Part part)
    {
        // Native player layers sort at their pivot. MeshRenderer instead sorts at
        // bounds.center: recalculating after posing moved the character behind scenery.
        // Keep the same sort point as the source while enclosing every posed vertex.
        var source=part.Source;
        var center=source.spriteSortPoint==SpriteSortPoint.Pivot?Vector3.zero:source.sprite.bounds.center;
        if(source.flipX) center.x=-center.x;
        if(source.flipY) center.y=-center.y;
        var extents=Vector3.zero;
        foreach(var vertex in part.Deformed)
        {
            var delta=vertex-center;
            extents=Vector3.Max(extents,new Vector3(Mathf.Abs(delta.x),Mathf.Abs(delta.y),Mathf.Abs(delta.z)));
        }
        part.Mesh.bounds=new Bounds(center,extents*2);
    }
    private static void Build(Part part,Sprite sprite)
    {
        var vertices=sprite.vertices; var uv=sprite.uv; var triangles=sprite.triangles;
        var output=new List<Vector3>(); var coords=new List<Vector2>();
        const int n=10;
        for(int t=0;t<triangles.Length;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
            void AddPoint(int i,int j)
            {
                float u=(float)i/n,v=(float)j/n;
                output.Add(vertices[a]*(1-u-v)+vertices[b]*u+vertices[c]*v);
                coords.Add(uv[a]*(1-u-v)+uv[b]*u+uv[c]*v);
            }
            for(int i=0;i<n;i++) for(int j=0;j<n-i;j++)
            {
                AddPoint(i,j); AddPoint(i+1,j); AddPoint(i,j+1);
                if(i+j<n-1) { AddPoint(i+1,j); AddPoint(i+1,j+1); AddPoint(i,j+1); }
            }
        }
        part.Original=output.ToArray(); part.Deformed=new Vector3[output.Count]; part.Colors=new Color[output.Count];
        int[] indices=new int[output.Count]; for(int i=0;i<indices.Length;i++) indices[i]=i;
        part.Mesh.Clear(); part.Mesh.vertices=part.Original; part.Mesh.uv=coords.ToArray(); part.Mesh.triangles=indices;
        part.Sprite=sprite;
    }
    internal void Restore()
    {
        foreach(var part in parts)
        {
            if(part.Source!=null) part.Source.forceRenderingOff=part.Hidden;
            if(part.Display!=null) { part.Display.enabled=false; Destroy(part.Display.gameObject); }
            if(part.Mesh!=null) Destroy(part.Mesh);
        }
        parts.Clear(); hat=null; basis=null;
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
