using UnityEngine;
namespace GameActions
{
    public sealed class CombatEffectLease:MonoBehaviour
    {
        public float Until;
        void Update(){if(Time.time>=Until)gameObject.SetActive(false);}
        void OnDisable(){var source=GetComponent<AudioSource>();if(source)source.Stop();}
    }
}
