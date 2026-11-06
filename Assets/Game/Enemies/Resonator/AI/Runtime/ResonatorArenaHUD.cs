using UnityEngine;
using UnityEngine.InputSystem;
namespace Resonator.AI
{
    public sealed class ResonatorArenaHUD : MonoBehaviour
    {
        public ResonatorPlayerBridge player;
        public ResonatorEnemy[] enemies;
        void Update(){if(Keyboard.current!=null&&Keyboard.current.nKey.wasPressedThisFrame&&player)player.EmitTestNoise();}
        void OnGUI()
        {
            GUI.Box(new Rect(12,12,400,65+enemies.Length*25),"Resonator AI test | N: noise | Esc: cursor");
            GUI.Label(new Rect(24,38,375,25),"LMB: punch / charge   RMB: guard   R: restore HP");
            for(int i=0;i<enemies.Length;i++)if(enemies[i])GUI.Label(new Rect(24,66+i*25,375,24),enemies[i].initialActivity+" : "+enemies[i].State+"  f"+enemies[i].CurrentFrame.ToString("0")+" HP "+enemies[i].Health.ToString("0"));
            if(player&&player.actor&&player.actor.vitals)GUI.Label(new Rect(24,65+enemies.Length*25,370,25),"Player HP "+player.actor.vitals.Health.ToString("0"));
        }
        void LateUpdate(){if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame&&player&&player.actor.vitals)player.actor.vitals.Restore();}
    }
}
