using System;
using UnityEngine;
namespace GameActions
{
    [Serializable]public sealed class CombatEffectPrefab
    {
        public CombatEffect kind;public GameObject prefab;[Min(.05f)]public float lifetime=2;
    }
    [CreateAssetMenu(menuName="Game Actions/Combat VFX")]
    public sealed class CombatEffectLibrary:ScriptableObject
    {
        public CombatEffectPrefab[] effects=Array.Empty<CombatEffectPrefab>();
        public CombatEffectPrefab Find(CombatEffect kind)=>Array.Find(effects,e=>e!=null&&e.kind==kind&&e.prefab);
    }
}
