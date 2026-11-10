using UnityEngine;
namespace GameActions
{
    [DisallowMultipleComponent]
    public sealed class GauntletSurfaceVFX:MonoBehaviour
    {
        public GauntletImpactVFXProfile profile;
        public int ContactsPlayed{get;private set;}
        public SoundSurface LastSurface{get;private set;}
        public GameObject LastDecal{get;private set;}
        public void Contact(Collider collider,Vector3 point,Vector3 normal)
        {
            if(!collider)return;
            if(!profile)profile=Resources.Load<GauntletImpactVFXProfile>("GauntletImpactVFX");
            if(!profile)return;
            LastSurface=SurfaceAudio.Kind(collider);ContactsPlayed++;
            switch(LastSurface)
            {
                case SoundSurface.Metal:
                    CombatEffects.Burst(profile.metalSpark,point+normal*.015f,normal,profile.sparkScale,2);break;
                case SoundSurface.Flesh:
                    CombatEffects.Burst(profile.bloodMist,point,normal,profile.bloodScale,4);
                    CombatEffects.Burst(profile.crystalFragments,point,normal,profile.crystalScale,8);break;
                case SoundSurface.Crystal:
                    CombatEffects.Burst(profile.crystalFragments,point,normal,profile.crystalScale,8);break;
                case SoundSurface.Concrete:
                    CombatEffects.Burst(profile.concreteDebris,point+normal*.015f,normal,profile.concreteDebrisScale,8);
                    if(profile.concreteCracks==null||profile.concreteCracks.Length==0)break;
                    var prefab=profile.concreteCracks[Random.Range(0,profile.concreteCracks.Length)];
                    float diameter=profile.referenceCraterDiameter*profile.concreteDecalScale;
                    LastDecal=CombatEffects.Burst(prefab,point,normal,diameter,profile.decalLifetime);
                    if(LastDecal){var crack=LastDecal.GetComponent<GroundCrackImpact>();if(crack){crack.lifetime=profile.decalLifetime;crack.SpawnCrack(point,normal,diameter,profile.concreteDepth);}}
                    break;
            }
        }
    }
}
