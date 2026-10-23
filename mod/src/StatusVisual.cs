using System;
using System.Collections.Generic;
using DolocTown;
using UnityEngine;

namespace Yinch.CelesteMovement;

// Run before the crouch/climb mesh copies the source material/property block.
[DefaultExecutionOrder(-100)]
public sealed class StatusVisual : MonoBehaviour
{
    private BodyController body;
    private readonly List<Part> parts=new List<Part>();
    private readonly MaterialPropertyBlock paint=new MaterialPropertyBlock();
    private sealed class Part
    {
        internal SpriteRenderer Source;
        internal Material Material;
        internal bool Enabled, Applied;
        internal readonly MaterialPropertyBlock Properties=new MaterialPropertyBlock();
    }
    internal void Bind(BodyController value) { body=value; enabled=true; }
    private Part Remember(SpriteRenderer source)
    {
        if(source==null) return null;
        var part=parts.Find(p=>p.Source==source);
        if(part==null) { part=new Part{Source=source}; parts.Add(part); }
        if(!part.Applied)
        {
            part.Enabled=source.enabled; part.Material=source.sharedMaterial;
            source.GetPropertyBlock(part.Properties); part.Applied=true;
        }
        return part;
    }
    private void Paint(SpriteRenderer source,Color color,float intensity)
    {
        if(source==null||!source.enabled||source.sprite==null) return;
        Remember(source);
        source.GetPropertyBlock(paint);
        // Reuse the native tint shader; change per-renderer uniforms only.
        // No shared material or texture pixels are modified.
        paint.SetColor("_BaseColor",color);
        paint.SetFloat("_Intensity",intensity);
        source.sharedMaterial=LocMaterials.GAME_MAT_HIT;
        source.SetPropertyBlock(paint);
    }
    private void Update() => Restore();
    private void LateUpdate()
    {
        try
        {
            var state=MovementRuntime.Current(body);
            var effect=state?.Feedback;
            if(effect==null) { Restore(); return; }
            var basis=(SpriteRenderer)body.GetComponent(typeof(SpriteRenderer));
            var split=body.SplitRenderer;
            // Whole-character overrides may not have matching separate layers.
            if(!effect.UseNativeHair&&split!=null&&!DolocAPI.modManager.HasPlayerOverride&&
                split.BodyRenderer.sprite!=null&&split.HairRenderer.sprite!=null)
            {
                Remember(basis); Remember(split.BodyRenderer); Remember(split.HairRenderer);
                if(basis.enabled) { basis.enabled=false; split.BodyRenderer.enabled=true; }
                // Keep the native hide-hair decision made by the equipped hat.
                Paint(split.HairRenderer,new Color(effect.R,effect.G,effect.B,1),effect.FlashWhite?1:.85f);
            }
            if(effect.FlashRed)
            {
                Paint(basis,Color.red,.8f);
                if(split!=null) Paint(split.BodyRenderer,Color.red,.8f);
                if(body.HatRenderer!=null)
                    foreach(var component in body.HatRenderer.GetComponentsInChildren(typeof(SpriteRenderer),true))
                        Paint((SpriteRenderer)component,Color.red,.8f);
            }
        }
        catch(Exception error)
        {
            Restore(); enabled=false;
            MovementRuntime.Monitor?.Log("Status visual error: "+error,DTMAPI.Abstractions.LogLevel.Error);
        }
    }
    internal void Restore()
    {
        foreach(var part in parts)
        {
            if(!part.Applied) continue;
            if(part.Source!=null)
            {
                part.Source.enabled=part.Enabled;
                part.Source.sharedMaterial=part.Material;
                part.Source.SetPropertyBlock(part.Properties);
            }
            part.Applied=false;
        }
        parts.RemoveAll(p=>p.Source==null);
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
