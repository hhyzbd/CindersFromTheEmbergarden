using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Embergarden
{

    internal class SubturretGizmo : Gizmo
    {
        public SubturretGizmo(CompMultipleTurretGun comp)
        {
            this.comp = comp;
            this.subTurrets = comp.turrets;
            this.subTurret = comp.turrets.Find(t=>t.ID ==comp.currentTurret);
            this.Order = -80f;
        }
        
        CompMultipleTurretGun comp;
        private List<SubTurret> subTurrets;
        private SubTurret subTurret;
        private static readonly CachedTexture ToggleTurretIcon = new CachedTexture("UI/Icon/ToggleTurret");
        private static readonly CachedTexture ForceAttack = new CachedTexture("UI/Commands/Attack");
        private bool drawRadius = true;

        public override bool Visible
        {
            get
            {
                return Find.Selector.SelectedPawns.Count == 1;
            }
        }
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outline = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect inner = outline.ContractedBy(6f);
            GUI.color = (parms.lowLight ? Command.LowLightBgColor : Color.white);
            GenUI.DrawTextureWithMaterial(outline, Command.BGTex, null, default(Rect));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            bool onGizmo = false;
            if (Mouse.IsOver(outline)) onGizmo = true;

            TaggedString taggedString = new TaggedString();
            //add text here
            taggedString += subTurret.ID;
            taggedString = taggedString.Truncate(inner.width, null);
            Vector2 vector = Text.CalcSize(taggedString);
            Rect turretNameRect = inner;
            turretNameRect.width = inner.width;
            turretNameRect.height = vector.y;
            
            Widgets.Label(turretNameRect, taggedString);
            if (Mouse.IsOver(turretNameRect))
            {
                Widgets.DrawHighlight(turretNameRect);

            }
            if (Widgets.ButtonInvisible(turretNameRect, false))
            {
                Find.WindowStack.Add(new FloatMenu(GetTurretOptions().ToList<FloatMenuOption>()));
            }

            //好丑，不会设计UI呜呜呜

            float weaponSize = inner.height - turretNameRect.height;
            Rect weaponRect = new Rect(inner.x, inner.y +turretNameRect.height,40f,40f);
            Texture2D icon= this.subTurret.turret.def.uiIcon;
            
            Widgets.DrawTextureFitted(weaponRect, icon, 1f);

            if (Mouse.IsOver(weaponRect))
            {
                drawRadius = true;
                
                Widgets.DrawHighlight(weaponRect);
            }
            else drawRadius = false;
            
            if (Widgets.ButtonInvisible(weaponRect, false))
            {
                Find.WindowStack.Add(new Dialog_InfoCard(subTurret.turret.def));
            }
            //

            Rect autofireRect = new Rect(weaponRect);
            autofireRect.x += weaponRect.width;
            Widgets.DrawTextureFitted(autofireRect, ToggleTurretIcon.Texture, 1f);
            bool autofire = Widgets.ButtonInvisible(autofireRect);

            Rect rect = new Rect(autofireRect.x + autofireRect.width - 15f, autofireRect.y, 15f, 15f);
            Texture2D image;
            image = subTurret.fireAtWill ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
            if (subTurret.TurretProp.autoAttack ==  true)
            {   
                GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit);
                
                if (autofire)
                {
                    subTurret.SwitchAutoFire();
                }
            }
            else
            {
                GUI.DrawTexture(rect, TexCommand.ClearPrioritizedWork, ScaleMode.ScaleToFit);
            }
            if (Mouse.IsOver(autofireRect))
            {
                Widgets.DrawHighlight(autofireRect);
            }
            //


            Rect targetRect = new Rect(autofireRect);
            targetRect.x += autofireRect.width + 5f;
            if (subTurret.forcedTarget == LocalTargetInfo.Invalid)
            {
                DrawSubGizmo(targetRect, ForceAttack.Texture, delegate () { subTurret.Targetting(); });
            }
            else
            {
                DrawSubGizmo(targetRect, TexCommand.ClearPrioritizedWork, delegate () { subTurret.ClearTarget();});
            }

            //
            return new GizmoResult(onGizmo ? GizmoState.Mouseover : GizmoState.Clear);
        }

        public override void GizmoUpdateOnMouseover()
        {
            if (!this.drawRadius)
            {
                return;
            }
            subTurret.CurrentEffectiveVerb.verbProps.DrawRadiusRing(subTurret.CurrentEffectiveVerb.caster.Position);
        }

        private void DrawSubGizmo(Rect rect,Texture tex, Action action)
        {
            Widgets.DrawTextureFitted(rect, tex, 1f);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            if (Widgets.ButtonInvisible(rect))
            {
                action.Invoke();
            }
        }

        public override float GetWidth(float maxWidth)
        {
            return 140f;
        }

        private IEnumerable<FloatMenuOption> GetTurretOptions()
        {
            if (subTurrets == null) yield break;
            foreach (var turret in this.subTurrets)
            {
                string text = turret.ID;
                yield return new FloatMenuOption(text, delegate ()
                {
                    comp.currentTurret = turret.ID;
                });
            }
            yield break;
        }
    }
}