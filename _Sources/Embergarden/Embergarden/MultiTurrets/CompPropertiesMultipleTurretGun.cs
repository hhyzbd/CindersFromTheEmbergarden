using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Embergarden
{
	public class CompProperties_MultipleTurretGun : CompProperties
	{
		public CompProperties_MultipleTurretGun()
		{
			this.compClass = typeof(CompMultipleTurretGun);
		}

		public List<SubTurretProperties> subTurrets;
	}

	public class SubTurretProperties
	{
		[NoTranslate]
		public string ID;
		public ThingDef turret;
		public float IdleAngleOffset;
		public float angleOffset;
		public bool autoAttack = true;
		public float warmingTime = 3f;
		public List<PawnRenderNodeProperties> renderNodeProperties;
    }
}
