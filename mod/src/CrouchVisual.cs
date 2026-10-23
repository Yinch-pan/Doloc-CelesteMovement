using System;
using System.Collections.Generic;
using DolocTown;
using UnityEngine;

namespace Yinch.CelesteMovement;

// Compatibility fallback for an alternate animator without touch_ground.
// The regular player now uses the game's actual layered bent-knee frame.
public sealed class CrouchVisual : MonoBehaviour
{
    private BodyController body;
    private readonly List<Part> parts=new List<Part>();
    private readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
    private sealed class Part
    {
        internal SpriteRenderer Source, Display;
        internal bool OriginalHidden;
    }
    internal void Bind(BodyController value) { body=value; enabled=true; }
    private void Add(SpriteRenderer source)
    {
        if(source==null || parts.Exists(p=>p.Source==source)) return;
        var go=new GameObject("CelesteCrouchVisual");
        go.layer=source.gameObject.layer;
        go.transform.SetParent(source.transform,false);
        var display=(SpriteRenderer)go.AddComponent(typeof(SpriteRenderer));
        parts.Add(new Part{Source=source,Display=display,OriginalHidden=source.forceRenderingOff});
    }
    private void LateUpdate()
    {
        try
        {
            var state=MovementRuntime.Current(body);
            if(state==null || !state.Motor.Ducking || state.HasNativeCrouchPose) { Restore(); return; }
            if(parts.Count==0)
            {
                Add((SpriteRenderer)body.GetComponent(typeof(SpriteRenderer)));
                if(body.SplitRenderer!=null)
                {
                    Add((SpriteRenderer)body.SplitRenderer.transform.parent.GetComponent(typeof(SpriteRenderer)));
                    Add(body.SplitRenderer.BodyRenderer);
                    Add(body.SplitRenderer.HairRenderer);
                }
                if(body.HatRenderer!=null)
                    foreach(var renderer in body.HatRenderer.GetComponentsInChildren(typeof(SpriteRenderer),true))
                        Add((SpriteRenderer)renderer);
                if(MovementRuntime.Config.DiagnosticLogging)
                    MovementRuntime.Monitor.Log("Crouch visual active; sprite layers="+parts.Count);
            }
            foreach(var part in parts)
            {
                var source=part.Source; var display=part.Display;
                if(source==null || display==null) continue;
                display.enabled=source.enabled&&!part.OriginalHidden;
                display.sprite=source.sprite; display.sharedMaterial=source.sharedMaterial;
                display.color=source.color; display.flipX=source.flipX; display.flipY=source.flipY;
                display.sortingLayerID=source.sortingLayerID; display.sortingOrder=source.sortingOrder;
                display.maskInteraction=source.maskInteraction;
                source.GetPropertyBlock(properties); display.SetPropertyBlock(properties);
                const float height=6f/11f;
                var anchor=source.transform.InverseTransformPoint(state.FeetWorld);
                display.transform.localPosition=new Vector3(0,anchor.y*(1-height),0);
                display.transform.localScale=new Vector3(1,height,1);
                source.forceRenderingOff=true;
            }
        }
        catch(Exception error)
        {
            Restore(); enabled=false;
            MovementRuntime.Monitor?.Log("Crouch visual error: "+error,DTMAPI.Abstractions.LogLevel.Error);
        }
    }
    internal void Restore()
    {
        foreach(var part in parts)
        {
            if(part.Source!=null) part.Source.forceRenderingOff=part.OriginalHidden;
            if(part.Display!=null)
            {
                part.Display.enabled=false;
                Destroy(part.Display.gameObject);
            }
        }
        parts.Clear();
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
