using Godot;
using Godot.Collections;

using System.Collections.Generic;
using System.Linq;

public partial class DeformHandler : Node3D
{
    [Export]
    public Camera3D camera;

    [Export]
    public float raycastSearchDistance = 4f;

    [Export]
    public float drawScale = 2f;
    private int steps = 20;  // Number of steps for interpolation of debug curve

    [Export]
    public MeshInstance3D meshInstance3D;

    RaycastHitInfo closestPointInfo;

    MeshDeformer meshDeformer;

    public override void _Ready()
    {  
        meshDeformer = new MeshDeformer();
    }

    public override void _Process(double delta)
    {
        // if(meshDeformer == null)
        // {
        //     GetTree().Root.GetNode("TerrainGenerator").GetNode<StaticBody3D>("*").GetNode<MeshInstance3D>;
        // }
        // DebugDraw3D.DrawArrow(camera.GlobalPosition, camera.GlobalPosition+Vector3.Forward*10, Colors.BlueViolet, 0.5f, true);
        
        // DebugDraw3D.DrawLine(camera.GlobalPosition, camera.GlobalPosition+(camera.GlobalTransform.Basis*Vector3.Forward)*3, Colors.Fuchsia);
        Vector3 cameraPosition = camera.GlobalPosition;

        List<RaycastHitInfo> raycastHits = GetAllCollisionsAlongRay(camera.GlobalPosition, 
            camera.GlobalPosition+(camera.GlobalTransform.Basis*Vector3.Forward)*3);
        closestPointInfo = null;
        if (raycastHits.Count == 0)
            return;
        // GD.Print("Hit found, assigning closest point");
        closestPointInfo = raycastHits[0];

        // Check if the left mouse button is pressed
        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Print something when left mouse button is clicked
            GD.Print("Left click detected with Hit found on closest point!");
            DebugDraw3D.DrawArrow(camera.GlobalPosition, camera.GlobalPosition+(camera.GlobalTransform.Basis*Vector3.Forward)*3, Colors.Fuchsia, 0.5f, true);
            DebugDraw3D.DrawLine(closestPointInfo.Position, closestPointInfo.Position + closestPointInfo.Normal, Colors.Teal);
        }
        // DebugDraw3D.DrawArrow(closestPointInfo.Position, closestPointInfo.Position + closestPointInfo.Normal, Colors.BlueViolet, 0.5f, true);
        // foreach(RaycastHitInfo raycastHitInfo in raycastHits)
        // {
        //     // DebugDraw3D.DrawLine(raycastHitInfo.Position, raycastHitInfo.Position+raycastHitInfo.Normal, Colors.Fuchsia);
        //     if(raycastHitInfo.Collider.GetParent().GetGroups().Contains("NavSource"))
        //     {
        //         closestPointInfo = raycastHitInfo;
        //         break;
        //     }
        // }

        // if(meshInstance3D != null)//precleanup
        // {
        //     RemoveChild(meshInstance3D);
        //     meshInstance3D._ExitTree();
        // }

        if(closestPointInfo == null)
        {
            GD.Print("No closest point on raycast, aborting draw");
            return;
        }

        Basis normalBasis = new Basis(new Quaternion(Vector3.Up, closestPointInfo.Normal));
        // Example: Draw a simple Bézier curve or any other kind of curve
        Vector3[] points = new Vector3[]//hexgonal-ish
        {
            new Vector3(10, 0, 10),
            new Vector3(10, 0, -10),
            new Vector3(-10, 0, 10),
            new Vector3(-10, 0, -10),
            new Vector3(12, 0, 5),
            new Vector3(-12, 0, 5),
        };


        for(int i = 0; i < points.Length; i++)
        {
            points[i] = normalBasis*points[i] + cameraPosition;
        }


        if (Input.IsMouseButtonPressed(MouseButton.Right))
        {
            // Print something when left mouse button is clicked
            GD.Print("Right click detected with Hit found on closest point!");
            // var mesh = new ArrayMesh();
            // mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, new BoxMesh().GetMeshArrays());
            // var mdt = new MeshDataTool();
            // mdt.CreateFromSurface(mesh, 0);


            MeshInstance3D collisionMesh = (MeshInstance3D)(closestPointInfo.Collider.GetParent());
            if(collisionMesh.Mesh.GetRid().Id != meshDeformer.meshIdFromCollider)
            {
                meshDeformer.AssignMeshDataTool(collisionMesh.Mesh);
            }
            meshDeformer.IndentVerticesInRadius(collisionMesh, closestPointInfo.Position, closestPointInfo.Position - cameraPosition, drawScale);

            // mesh.ClearSurfaces();
            // mdt.CommitToSurface(mesh);
            // meshInstance3D.Mesh = mesh;
        }
    }

    

    // Simple quadratic Bezier curve function
    private Vector3 BezierCurve(float t, Vector3[] points)
    {
        Vector3 p0 = points[0];
        Vector3 p1 = points[1];
        Vector3 p2 = points[2];
        
        // Quadratic Bezier formula
        return (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
    }

    public class RaycastHitInfo
    {
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
        public CollisionObject3D Collider { get; set; }
        public float Distance { get; set; }
    }

    private string Collider = "collider";

    public List<RaycastHitInfo> GetAllCollisionsAlongRay(//crash when hitting non Navmesh?
        Vector3 from,
        Vector3 to,
        uint collisionMask = 2,//2nd bit for generated terrain mesh
        int maxResults = 3)
    {

        // var currentObstructions = new List<Node3D>();
        // var currentObstructionRids = new Godot.Collections.Array<Rid>();

        var hits = new List<RaycastHitInfo>();
        // var spaceState = PhysicsServer3D.SpaceGetDirectState(GetWorld3D().Space);
        var spaceState = GetWorld3D().DirectSpaceState;
        var queryParams = new PhysicsRayQueryParameters3D
        {
            From = from,
            To = to,
            CollisionMask = collisionMask,
            CollideWithAreas = true,
            CollideWithBodies = true,
            // Exclude = currentObstructionRids
        };
        // GD.Print("Raycasting via IntersectRay");
        // Continue raycasting until we hit the maximum number of results or reach the end
        var result = spaceState.IntersectRay(queryParams);

        
        while (result.Count > 0)
        {

            // No more collisions found
            var hitPosition = (Vector3)result["position"];
            var hitNormal = (Vector3)result["normal"];
            var hitCollider = result["collider"].As<CollisionObject3D>();
            var hitDistance = from.DistanceTo(hitPosition);


            // TerrainMeshComponent coc = hitCollider.GetNode<TerrainMeshComponent>("TerrainMeshComponent");

            // // Adjust the ray start position slightly past the hit point for the next iteration
            // currentObstructionRids.Add(hitCollider.GetRid());
            // queryParams.Exclude = currentObstructionRids;
            // currentObstructions.Add(hitCollider);

            // if (!_transparentObjects.Contains(hitCollider))
            // {
            //     // MAKE TRANSPARENT
            //     coc.MakeTranparent();
            //     _transparentObjects.Add(hitCollider);
            // }

            // if (hitCollider.GetGroups().Contains("TerrainMesh"))
            // {
                hits.Add(new RaycastHitInfo
                {
                    Position = hitPosition,
                    Normal = hitNormal,
                    Collider = hitCollider,
                    Distance = hitDistance
                });
                break;
            // }


            result = spaceState.IntersectRay(queryParams);
            GD.Print("result.Count: ", result.Count);
        }

        // foreach (var obj in _transparentObjects)
        // {
        //     if (!currentObstructions.Contains(obj))
        //     {
        //         if (obj.HasNode("TerrainMeshComponent"))
        //         {
        //             obj.GetNode<TerrainMeshComponent>("TerrainMeshComponent").MakeOpaque();
        //         }
        //     }
        // }

        // _transparentObjects = currentObstructions;

        


        // Sort hits by distance from origin
        // hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return hits;
    }
}