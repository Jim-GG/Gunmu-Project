using System;using System.Linq;using System.Reflection;using System.Collections;using UnityEngine;
namespace AssetPipeline.Editor
{
 internal static class GraphReflect
 {
  internal const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  internal static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null)??throw new Exception("Missing graph type "+name);
  internal static object Get(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var p=t.GetProperty(n,F|BindingFlags.DeclaredOnly);if(p!=null)return p.GetValue(o);var f=t.GetField(n,F|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(o);}throw new Exception(o.GetType()+" has no "+n);}
  internal static void Set(object o,string n,object v){for(var t=o.GetType();t!=null;t=t.BaseType){var p=t.GetProperty(n,F|BindingFlags.DeclaredOnly);if(p!=null&&p.CanWrite){p.SetValue(o,Convert(v,p.PropertyType));return;}var f=t.GetField(n,F|BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(o,Convert(v,f.FieldType));return;}}throw new Exception(o.GetType()+" has no writable "+n);}
  static object Convert(object a,Type t)=>t.IsEnum&&a is string s?Enum.Parse(t,s):a;
  static bool Fits(ParameterInfo[] p,object[] a)=>p.Length>=a.Length&&p.Skip(a.Length).All(x=>x.IsOptional)&&a.Select((x,i)=>x==null||p[i].ParameterType.IsInstanceOfType(x)||p[i].ParameterType.IsEnum&&x is string).All(x=>x);
  static object[] Args(ParameterInfo[] p,object[] a)=>p.Select((x,i)=>i<a.Length?Convert(a[i],x.ParameterType):x.DefaultValue).ToArray();
  internal static object New(string type,params object[] args){var t=T(type);var c=t.GetConstructors(F).FirstOrDefault(x=>Fits(x.GetParameters(),args))??throw new Exception("No constructor "+type+" / "+args.Length);return c.Invoke(Args(c.GetParameters(),args));}
  internal static object Call(object o,string n,params object[] a){var t=o as Type??o.GetType();for(var q=t;q!=null;q=q.BaseType){var m=q.GetMethods(F|BindingFlags.DeclaredOnly).FirstOrDefault(x=>x.Name==n&&!x.ContainsGenericParameters&&Fits(x.GetParameters(),a));if(m!=null)return m.Invoke(o is Type?null:o,Args(m.GetParameters(),a));}throw new Exception("No method "+t+"."+n+" / "+a.Length);}
  internal static object[] List(object value)=>(value as IEnumerable)?.Cast<object>().ToArray()??Array.Empty<object>();
  internal static object Field(string type,string name)=>T(type).GetField(name,F).GetValue(null);
  internal static void Place(object node,float x,float y){var state=Get(node,"drawState");Set(state,"position",new Rect(x,y,230,160));Set(node,"drawState",state);}
 }
}
