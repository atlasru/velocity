using NUnit.Framework;
using Velocity.Core;

namespace Velocity.Tests
{
    public sealed class CollisionMeshTests
    {
        [Test]
        public void RenderedRampTrianglesLieOnItsCollisionPlanes()
        {
            ConvexBrush b=BrushFactory.Ramp(new Vec3(5,3,2),14,40,55,1,35);
            foreach(Vec3 vertex in b.Vertices)
            {
                bool onPlane=false;
                foreach(CollisionPlane plane in b.Planes)
                {
                    float distance=Vec3.Dot(vertex,plane.Normal)-plane.Distance;
                    Assert.LessOrEqual(distance,.00002f);
                    if(System.Math.Abs(distance)<.00002f) onPlane=true;
                }
                Assert.IsTrue(onPlane);
            }
        }
    }
}
