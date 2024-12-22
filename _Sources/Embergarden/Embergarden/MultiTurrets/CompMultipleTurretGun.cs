using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace DMS
{
    public class CompMultipleTurretGun : ThingComp
    {
        public CompPropertiesMultipleTurretGun Props
        {
            get
            {
                return (CompPropertiesMultipleTurretGun)this.props;
            }
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                Props.subTurrets.ForEach(t =>
                {
                    SubTurret turret = new SubTurret() { ID = t.ID, parent = this.parent };
                    turret.Init(t);
                    this.turrets.Add(turret);
                });
                
            }
            turrets.RemoveDuplicates((a, b) => a.ID == b.ID);
            if (currentTurret == null)
            {
                currentTurret = turrets.First().ID;
            }
            
        }
        public override void CompTick()
        {
            base.CompTick();
            turrets.ForEach(t => t.Tick());
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (this.parent is Pawn pawn && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                yield return new SubturretGizmo(this);
            }
            
            yield break;
        }
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (!this.Props.subTurrets.NullOrEmpty())
            {
                foreach (SubTurretProperties t in this.Props.subTurrets)
                {
                    yield return new StatDrawEntry(StatCategoryDefOf.PawnCombat, "Turret".Translate(),t.turret.LabelCap, "Stat_Thing_TurretDesc".Translate(), 5600, null, Gen.YieldSingle<Dialog_InfoCard.Hyperlink>(new Dialog_InfoCard.Hyperlink(t.turret, -1)), false, false);
                }
            }
            yield break;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref this.turrets, "turrets", LookMode.Deep);
            Scribe_Values.Look(ref this.currentTurret,"currentTurrent");
            if (Scribe.mode==LoadSaveMode.PostLoadInit)
            {
                foreach (var t in turrets)
                {
                    t.Init(Props.subTurrets.Find(p => p.ID == t.ID));
                }
            }
        }

        public override List<PawnRenderNode> CompRenderNodes()
        {
            if (this.parent is Pawn pawn)
            {
                List<PawnRenderNode> list = new List<PawnRenderNode>();

                foreach (SubTurret t in this.turrets)
                {
                    list.AddRange(t.RenderNodes(pawn));
                }
                return list;
            }
            return base.CompRenderNodes();
        }

        public List<SubTurret> turrets = new List<SubTurret>(); 
        public string currentTurret;
    }
    [StaticConstructorOnStartup]
    public class SubTurret : IAttackTargetSearcher, IExposable
    {
        public Thing Thing => this.parent;

        public Verb CurrentEffectiveVerb => this.GunCompEq.PrimaryVerb;
        public CompEquippable GunCompEq
        {
            get
            {
                return this.turret.TryGetComp<CompEquippable>();
            }
        }

        public LocalTargetInfo LastAttackedTarget => this.lastAttackedTarget;

        public int LastAttackTargetTick => this.lastAttackTargetTick;
        private bool CanShoot
        {
            get
            {
                Pawn pawn;
                if ((pawn = (this.parent as Pawn)) != null)
                {
                    if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
                    {
                        return false;
                    }
                    if (pawn.stances.stunner.Stunned)
                    {
                        return false;
                    }
                    if (this.TurretDestroyed)
                    {
                        return false;
                    }
                    if (pawn.IsColonyMechPlayerControlled && !this.fireAtWill)
                    {
                        return false;
                    }
                }
                CompCanBeDormant compCanBeDormant = this.parent.TryGetComp<CompCanBeDormant>();
                return compCanBeDormant == null || compCanBeDormant.Awake;
            }
        }
        private bool WarmingUp
        {
            get
            {
                return this.burstWarmupTicksLeft > 0;
            }
        }
        public bool TurretDestroyed
        {
            get
            {
                Pawn pawn;
                return (pawn = (this.parent as Pawn)) != null && this.CurrentEffectiveVerb.verbProps.linkedBodyPartsGroup != null && this.CurrentEffectiveVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable && PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(pawn.health.hediffSet, this.CurrentEffectiveVerb.verbProps.linkedBodyPartsGroup) <= 0f;
            }
        }
        public void Init(SubTurretProperties prop)
        {
            this.turretProp = prop;
            if (this.turret == null)
            {
                this.turret = ThingMaker.MakeThing(this.turretProp.turret, null);
            }
            this.UpdateGunVerbs();
        }
        private void UpdateGunVerbs()
        {
            List<Verb> allVerbs = this.turret.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this.parent;
                verb.verbProps.warmupTime = 0;
                verb.castCompleteCallback = delegate ()
                {
                    this.burstCooldownTicksLeft = this.CurrentEffectiveVerb.verbProps.defaultCooldownTime.SecondsToTicks();
                };
            }
        }
        public void Tick()
        {
            if (this.CanShoot == false)
            {
                return;
            }
            
            if (CheckTarget())
            {
                this.curRotation = (this.currentTarget.Cell.ToVector3Shifted() - this.parent.DrawPos).AngleFlat() + this.turretProp.angleOffset;
            }
            else
            {
                this.curRotation = this.turretProp.IdleAngleOffset + parent.Rotation.AsAngle + this.turretProp.angleOffset;
            }
            this.CurrentEffectiveVerb.VerbTick();
            if (this.CurrentEffectiveVerb.state != VerbState.Bursting)
            {
                if (this.WarmingUp)
                {
                    this.burstWarmupTicksLeft--;
                    if (this.burstWarmupTicksLeft == 0)
                    {
                        this.CurrentEffectiveVerb.TryStartCastOn(this.currentTarget, this.currentTarget, false, true, false, true);
                        this.lastAttackTargetTick = Find.TickManager.TicksGame;
                        this.lastAttackedTarget = this.currentTarget;
                        return;
                    }
                }
                else
                {
                    if (this.burstCooldownTicksLeft > 0)
                    {
                        this.burstCooldownTicksLeft--;
                    }
                    if (this.burstCooldownTicksLeft <= 0 && this.parent.IsHashIntervalTick(10))
                    {
                        
                        if (this.turretProp.autoAttack && !this.forcedTarget.IsValid)
                        {
                            this.currentTarget = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, null, 0f, 9999f);
                        }
                        if (this.currentTarget.IsValid)
                        {
                            this.burstWarmupTicksLeft = this.turretProp.warmingTime.SecondsToTicks();
                            return;
                        }
                        
                        this.ResetCurrentTarget();
                    }
                }
            }
        }
        private bool CheckTarget()
        {
            if (!currentTarget.IsValid )
            {
                this.forcedTarget = LocalTargetInfo.Invalid;
                return false;
            }
            if (this.currentTarget.ThingDestroyed || (currentTarget.TryGetPawn(out var p) && p.DeadOrDowned))
            {
                this.currentTarget = LocalTargetInfo.Invalid;
                return false;
            }

            return true;
        }

        private void ResetCurrentTarget()
        {
            this.currentTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
        }
        public List<PawnRenderNode> RenderNodes(Pawn pawn)
        {
            List<PawnRenderNode> result = new List<PawnRenderNode>();
            turretProp.renderNodeProperties.ForEach(p =>
            {
                PawnRenderNode_SubTurretGun pawnRenderNode_TurretGun = (PawnRenderNode_SubTurretGun)Activator.CreateInstance(p.nodeClass, new object[]
                {
                        pawn,
                        p,
                        pawn.Drawer.renderer.renderTree
                });
                pawnRenderNode_TurretGun.subturret = this;
                result.Add(pawnRenderNode_TurretGun);
            });
            return result;
        }
        public void SwitchAutoFire()
        {
            this.fireAtWill = !this.fireAtWill;
        }

        public void Targetting()
        {
            
            var tar = Find.Targeter;
            
            tar.BeginTargeting(this.CurrentEffectiveVerb.targetParams,(t) => { this.forcedTarget = t; this.currentTarget = t; });
        }

        public void ClearTarget()
        {
            this.forcedTarget = LocalTargetInfo.Invalid;
            this.currentTarget = LocalTargetInfo.Invalid;
        }
        //

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.ID, "ID");

            Scribe_References.Look(ref this.parent, "parent");
            Scribe_Deep.Look(ref this.turret, "turret");

            Scribe_Values.Look<int>(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<int>(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_TargetInfo.Look(ref this.forcedTarget, "forcedTarget");
            Scribe_TargetInfo.Look(ref this.currentTarget, "currentTarget");

            Scribe_Values.Look<bool>(ref this.fireAtWill, "fireAtWill", true, false);
            
        }
        [NoTranslate]
        public string ID = "null";

        public Thing parent;
        public Thing turret;


        protected int burstCooldownTicksLeft;
        protected int burstWarmupTicksLeft;
        public LocalTargetInfo forcedTarget = LocalTargetInfo.Invalid;
        protected LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

        public bool fireAtWill = true;
        private LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;
        private int lastAttackTargetTick;
        public SubTurretProperties turretProp;

        public float curRotation;
    }
}
