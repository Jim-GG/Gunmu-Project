using UnityEngine;
namespace CrystalParkour
{
    public sealed class CrystalParkourHUD:MonoBehaviour
    {
        public CrystalTraversal player;public Font font;
        GUIStyle body,title;Texture2D pixel;
        void Awake(){if(!font)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Arial"},18);}
        void OnGUI()
        {
            if(!player)return;
            if(body==null){body=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true};body.normal.textColor=Color.white;title=new GUIStyle(body){fontSize=25,fontStyle=FontStyle.Bold};pixel=Texture2D.whiteTexture;}
            float scale=Mathf.Clamp(Screen.height/900f,.65f,1.6f);var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float width=Screen.width/scale,height=Screen.height/scale;
            GUI.color=new Color(.025f,.035f,.06f,.86f);GUI.DrawTexture(new Rect(20,20,710,145),pixel);GUI.color=Color.white;
            GUI.Label(new Rect(38,30,680,38),"零界 · 晶体冲击跑酷试验",title);
            GUI.Label(new Rect(38,72,670,52),"WASD 移动 / Shift 奔跑 / Space 跳跃\n按住左键瞄准晶体牵引，攀附后 2 秒内松开；R 返回检查点 / Esc 释放鼠标",body);
            GUI.Label(new Rect(38,126,670,28),player.Hint,body);
            GUI.color=new Color(.025f,.035f,.06f,.86f);GUI.DrawTexture(new Rect(20,height-85,950,65),pixel);GUI.color=Color.white;
            string Done(bool value)=>value?"✓":"○";
            GUI.Label(new Rect(38,height-76,920,45),$"{Done(player.GrewFloor)} 地面生晶    {Done(player.Rebounds>0)} 冲击反弹    {Done(player.UsedWind)} 风洞    {Done(player.GrewWall)} 墙面生晶    {Done(player.RodeDrone)} 无人机借力",body);
            float cx=width*.5f,cy=height*.5f;GUI.color=player.Target?new Color(.8f,.5f,1):Color.white;GUI.DrawTexture(new Rect(cx-6,cy-1,12,2),pixel);GUI.DrawTexture(new Rect(cx-1,cy-6,2,12),pixel);
            if(player.State==CrystalTraversal.Mode.Attached||player.State==CrystalTraversal.Mode.AwaitingImpact)
            {
                GUI.color=Color.black;GUI.DrawTexture(new Rect(cx-95,cy+44,190,10),pixel);GUI.color=player.Remaining>.5f?new Color(.65f,.4f,1):new Color(1,.28f,.12f);GUI.DrawTexture(new Rect(cx-95,cy+44,190*player.Remaining/Mathf.Max(.01f,player.attachmentSeconds),10),pixel);GUI.color=Color.white;GUI.Label(new Rect(cx-95,cy+62,350,30),player.State==CrystalTraversal.Mode.AwaitingImpact?"等待命中帧…":$"{player.Remaining:0.00}s · 松开左键",body);
            }
            if(player.Completed){GUI.color=new Color(.025f,.035f,.06f,.93f);GUI.DrawTexture(new Rect(cx-260,cy-100,520,150),pixel);GUI.color=Color.white;GUI.Label(new Rect(cx-230,cy-78,470,45),"实验完成 / TRAVERSAL COMPLETE",title);GUI.Label(new Rect(cx-230,cy-22,470,55),$"已完成全部五种交互，累计晶体反弹 {player.Rebounds} 次。",body);}
            GUI.color=Color.white;GUI.matrix=old;
        }
    }
}
