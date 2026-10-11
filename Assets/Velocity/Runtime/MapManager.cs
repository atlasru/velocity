using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Velocity.Core;

namespace Velocity.Runtime
{
    public sealed class MapManager : IDisposable
    {
        public readonly GameObject Root;
        private readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
        private readonly Material[] materials;
        private readonly List<Vector3> lineVertices=new List<Vector3>();
        private readonly List<Color> lineColors=new List<Color>();
        private readonly List<int> lineIndices=new List<int>();
        public static Vector3 Unity(Vec3 v) => new Vector3(v.X,v.Y,v.Z);
        public static Vec3 Core(Vector3 v) => new Vec3(v.x,v.y,v.z);
        public MapManager(MapDefinition map)
        {
            Root=new GameObject(map.Name);
            Color[] colors={new Color(.22f,.26f,.29f),new Color(.14f,.43f,.46f),new Color(.48f,.56f,.36f),new Color(.72f,.46f,.2f)};
            materials=new Material[colors.Length];
            for(int i=0;i<colors.Length;i++)
            {
                materials[i]=new Material(Shader.Find("Velocity/Surface")); materials[i].color=colors[i];
                resources.Add(materials[i]);
                var vertices=new List<Vector3>(); var indices=new List<int>();
                foreach(ConvexBrush brush in map.World.Brushes)
                {
                    if(brush.Material!=i) continue;
                    // Duplicate face vertices to preserve flat normals at brush edges.
                    foreach(int index in brush.Triangles) { indices.Add(vertices.Count); vertices.Add(Unity(brush.Vertices[index])); }
                }
                if(vertices.Count==0) continue;
                var mesh=new Mesh{name="Map batch "+i,indexFormat=IndexFormat.UInt32}; mesh.SetVertices(vertices); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); resources.Add(mesh);
                var batch=new GameObject("Geometry "+i); batch.transform.SetParent(Root.transform); batch.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=batch.AddComponent<MeshRenderer>(); renderer.sharedMaterial=materials[i]; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            }
            var lineMaterial=new Material(Shader.Find("Velocity/VertexUnlit")); resources.Add(lineMaterial);
            foreach(ConvexBrush brush in map.World.Brushes)
            {
                Vec3[] v=brush.Vertices;
                if(v.Length==6)
                {
                    Vec3 n=brush.Planes[1].Normal*.025f;Outline(new[]{Unity(v[0]+n),Unity(v[1]+n),Unity(v[2]+n),Unity(v[0]+n)},lineMaterial,new Color(.2f,.53f,.55f),.02f);continue;
                }
                if(v.Length!=8) continue;
                Vec3 offset=brush.Planes[1].Normal*.025f;
                Outline(new[]{Unity(v[4]+offset),Unity(v[5]+offset),Unity(v[6]+offset),Unity(v[7]+offset),Unity(v[4]+offset)},lineMaterial,new Color(.54f,.67f,.7f),.035f);
                if(brush.Material==1)
                {
                    for(int j=1;j<8;j++)
                    {
                        float t=j/8f; Vec3 a=Vec3.Lerp(v[4],v[7],t)+offset,b=Vec3.Lerp(v[5],v[6],t)+offset;
                        Outline(new[]{Unity(a),Unity(b)},lineMaterial,new Color(.2f,.53f,.55f),.016f);
                    }
                }
            }
            if(lineVertices.Count>0)
            {
                var mesh=new Mesh{name="All surface lines",indexFormat=IndexFormat.UInt32}; mesh.SetVertices(lineVertices); mesh.SetColors(lineColors); mesh.SetTriangles(lineIndices,0); mesh.RecalculateBounds(); resources.Add(mesh);
                var batch=new GameObject("Surface line batch");batch.transform.SetParent(Root.transform);batch.AddComponent<MeshFilter>().sharedMesh=mesh;batch.AddComponent<MeshRenderer>().sharedMaterial=lineMaterial;
            }
            foreach(MapZone zone in map.Zones)
            {
                if(zone.Kind==ZoneKind.Weapon)
                {
                    var pickup=GameObject.CreatePrimitive(PrimitiveType.Cube); pickup.name=WeaponCatalog.All[zone.Index].Name+" ammo pickup"; pickup.transform.SetParent(Root.transform);
                    pickup.transform.position=Unity(zone.Center); pickup.transform.localScale=new Vector3(.6f,.3f,.6f); UnityEngine.Object.Destroy(pickup.GetComponent<Collider>()); pickup.GetComponent<Renderer>().sharedMaterial=materials[3];
                }
                else
                {
                    string label=zone.Kind==ZoneKind.Start?"START":zone.Kind==ZoneKind.Finish?"FINISH":"CHECKPOINT "+zone.Index;
                    Label(label,Unity(zone.Center)+Vector3.up*1.8f,new Color(.75f,.82f,.79f),.15f);
                }
            }
            Label(map.Name.ToUpperInvariant(),Unity(map.Spawn)+new Vector3(0,4,9),Color.white,.3f);
            // Distant columns make orientation and elevation legible without textures or post-processing.
            for(int i=0;i<10;i++)
            {
                var column=GameObject.CreatePrimitive(PrimitiveType.Cube); column.transform.SetParent(Root.transform);
                column.transform.position=new Vector3((i%2==0?-1:1)*45,-35,i*24); column.transform.localScale=new Vector3(4,20+i*3,4);
                UnityEngine.Object.Destroy(column.GetComponent<Collider>()); column.GetComponent<Renderer>().sharedMaterial=materials[0];
            }
        }
        private void Outline(Vector3[] points,Material material,Color color,float width)
        {
            for(int i=0;i<points.Length-1;i++)
            {
                Vector3 direction=(points[i+1]-points[i]).normalized;
                Vector3 side=Vector3.Cross(direction,Vector3.up).normalized*width*.5f;
                if(side.sqrMagnitude<.0000001f) side=Vector3.right*width*.5f;
                int start=lineVertices.Count;
                lineVertices.Add(points[i]-side);lineVertices.Add(points[i]+side);lineVertices.Add(points[i+1]+side);lineVertices.Add(points[i+1]-side);
                for(int j=0;j<4;j++) lineColors.Add(color);
                lineIndices.Add(start);lineIndices.Add(start+1);lineIndices.Add(start+2);lineIndices.Add(start);lineIndices.Add(start+2);lineIndices.Add(start+3);
            }
        }
        private void Label(string text,Vector3 position,Color color,float size)
        {
            var o=new GameObject(text); o.transform.SetParent(Root.transform); o.transform.position=position;
            var label=o.AddComponent<TextMesh>(); label.text=text; label.fontSize=60; label.characterSize=size; label.anchor=TextAnchor.MiddleCenter; label.color=color;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");o.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
            o.transform.rotation=Quaternion.identity;
        }
        public void Dispose() { UnityEngine.Object.Destroy(Root); foreach(UnityEngine.Object resource in resources) UnityEngine.Object.Destroy(resource); }
    }
}
