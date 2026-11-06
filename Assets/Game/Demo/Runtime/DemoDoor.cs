using System.Collections;
using UnityEngine;
namespace NullBoundary.Demo
{
    public sealed class DemoDoor : MonoBehaviour
    {
        public Transform slab;
        public bool startsOpen;
        public float duration=.75f, travel=3.8f;
        public bool IsClosed { get; private set; }
        public bool IsMoving { get; private set; }
        Vector3 closedPosition;
        Coroutine motion;
        void Awake()
        {
            closedPosition=slab.localPosition;
            ResetDoor(startsOpen);
        }
        public void ResetDoor(bool open)
        {
            if (motion!=null) StopCoroutine(motion);
            IsMoving=false; IsClosed=!open;
            slab.localPosition=closedPosition+(open?Vector3.up*travel:Vector3.zero);
        }
        public void SetOpen(bool open)
        {
            if (IsMoving || IsClosed==!open) return;
            motion=StartCoroutine(Move(open));
        }
        IEnumerator Move(bool open)
        {
            IsMoving=true;
            var a=slab.localPosition;var b=closedPosition+(open?Vector3.up*travel:Vector3.zero);
            if (DemoFlow.Instance) DemoFlow.Instance.sound.Play(DemoSound.Cue.Door,transform.position);
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                slab.localPosition=Vector3.Lerp(a,b,Mathf.SmoothStep(0,1,t/duration));yield return null;
            }
            slab.localPosition=b;IsClosed=!open;IsMoving=false;
            if (IsClosed && DemoFlow.Instance && DemoFlow.Instance.chaseDoor==this)
                DemoFlow.Instance.ChaseDoorClosed();
        }
    }
}

