using UnityEngine;
using UnityEngine.UI;

namespace Velocity.Runtime
{
    public sealed class TouchGraphic : Graphic
    {
        public bool Ring=true;
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); const int segments=48;
            float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            for(int i=0;i<=segments;i++)
            {
                float angle=i*(Mathf.PI*2/segments); Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                helper.AddVert(d*radius,color,Vector2.zero); helper.AddVert(d*(Ring?radius-2:0),color,Vector2.zero);
                if(i>0) { int k=i*2;helper.AddTriangle(k-2,k-1,k);helper.AddTriangle(k,k-1,k+1); }
            }
        }
    }
}
