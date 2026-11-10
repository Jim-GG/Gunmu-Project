using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace AssetPipeline.Editor
{
    public static class RockGeometryUtility
    {
        sealed class Face {public Vector3 normal;public float plane,area;}
        public static Quaternion LargestFaceDown(Mesh mesh)
        {
            var faces=new List<Face>();float tolerance=Mathf.Max(.00001f,mesh.bounds.size.magnitude*.002f);
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh))
            using(var vertices=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
            {
                var data=snapshot[0];data.GetVertices(vertices);
                for(int part=0;part<data.subMeshCount;part++)
                using(var indices=new NativeArray<int>(data.GetSubMesh(part).indexCount,Allocator.Temp))
                {
                    data.GetIndices(indices,part);
                    for(int i=0;i+2<indices.Length;i+=3)
                    {
                        var a=vertices[indices[i]];var cross=Vector3.Cross(vertices[indices[i+1]]-a,vertices[indices[i+2]]-a);float twiceArea=cross.magnitude;if(twiceArea<.0000001f)continue;
                        var normal=cross/twiceArea;float plane=Vector3.Dot(normal,a);Face face=null;
                        foreach(var candidate in faces)if(Vector3.Dot(candidate.normal,normal)>.996f&&Mathf.Abs(candidate.plane-plane)<tolerance){face=candidate;break;}
                        if(face==null){face=new Face{normal=normal,plane=plane};faces.Add(face);}face.area+=twiceArea*.5f;
                    }
                }
            }
            Face biggest=null;foreach(var f in faces)if(biggest==null||f.area>biggest.area)biggest=f;
            return biggest==null?Quaternion.identity:Quaternion.FromToRotation(biggest.normal,Vector3.down);
        }
        public static Bounds WorldVertexBounds(Mesh mesh,Transform transform)
        {
            using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh))
            using(var vertices=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
            {
                snapshot[0].GetVertices(vertices);var b=new Bounds(transform.TransformPoint(vertices[0]),Vector3.zero);for(int i=1;i<vertices.Length;i++)b.Encapsulate(transform.TransformPoint(vertices[i]));return b;
            }
        }
    }
}
