using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Godot;

public partial class CaveTestRRT : Node3D
{

    [Export] float rangeMultiplier = 100f;
    [Export] Vector2 x_range = new Vector2(-1, 1);
    [Export] Vector2 y_range = new Vector2(-1, 0);
    [Export] Vector2 z_range = new Vector2(-1, 1);
    [Export] Vector2 radius_range = new Vector2(.5f, 1);
    [Export] Vector2 height_range = new Vector2(1, 3);

    // [Export]
    // Vector3 main_room_dimension_min = new Vector3(5, 5, 5);
    // [Export]
    // Vector3 main_room_dimension_max = new Vector3(10, 10, 10);
    [Export] Vector2 main_room_length_range = new Vector2(2, 4);
    [Export] Vector2 main_room_radius_range = new Vector2(4, 8);

    [Export] float branching_factor = .3f;//probability of branching, could also do some curve with domain pow of depth

    [Export] float depthResetRadius = 1f;//reset depth value also on non-branch if endpoint is close to another
    [Export] Vector2 RadialLacunarityRange = new Vector2(0, 2f);//opposing
    [Export] Vector2 AngularLacunarityRange = new Vector2(0, .2f);//Values should be clamped to -1, 1, mul by Mathf.Pi, attraction

    [Export] Curve LacunarityCurve;//defines a pda for opposing/attracting invariants
    [Export] float LacunarityDecay = .7f;

    [Export] Vector2 branch_depth_meander_range = new Vector2(1, 3);

    [Export] public int[] MainRoomSpawnIntervals;
    [Export] public int[] MainRoomOrientedUp;

    // [Export]
    // Vector3 Drift = Vector3.Down;

    [Export] bool Branching;
    [Export] bool anastomosing;
    [Export] bool anastomosingLeafLimit;
    [Export] int numBranches;

    [Export] Vector3 RootPosition = Vector3.Up * 58f;

    public List<CaveCapsuleInfo> _Lcci;

    [Export] public CylinderMeshGenerator cylinderMeshGenerator;


    // DebugDraw3D.
    private float RangeLength(Vector2 v2)
    {
        return Mathf.Abs(v2.X - v2.Y);
    }

    private bool InRange(Vector2 v2, float f)
    {
        if (v2.X > v2.Y)
        {
            GD.Print("WARNING range min > max");
        }
        return v2.X <= f && v2.Y >= f;
    }

    public CaveCapsuleInfo Nearest(Vector3 v3, List<CaveCapsuleInfo> llv)
    {
        CaveCapsuleInfo Nearest = llv[0];

        float distance = Mathf.Inf;
        foreach (var cci in llv)
        {

            float _distance = (cci.EndPosition - v3).Length();
            if (_distance < distance)
            {
                Nearest = cci;
                distance = _distance;
            }
        }
        return Nearest;
    }


    public void UpdateCaveCapsuleInfosInRange(CaveCapsuleInfo cci, List<CaveCapsuleInfo> llv)
    {
        List<CaveCapsuleInfo> editedCCis = new List<CaveCapsuleInfo>();
        foreach (var _cci in llv)
        {
            float radius_sum = _cci.Radius + cci.Radius;
            if ((_cci.EndPosition - cci.StartPosition).Length() <= radius_sum)
            {
                if (!_cci.Children.Contains(cci))
                {
                    _cci.Children.Add(cci);
                    cci.AddSelfToParentChildren();
                }
            }
            else if ((_cci.StartPosition - cci.EndPosition).Length() <= radius_sum)
            {
                if (!_cci.Parents.Contains(cci))
                {
                    _cci.Parents.Add(cci);
                    _cci.AddSelfToParentChildren();
                }
            }
            else
            {
                continue;
            }
            editedCCis.Add(_cci);
            //TODO handle near intersections?
        }
    }

    // public void PropagateBranchDepths()
    // {

    // }

    // private Vector3 GenerateMainRoomDimensions()
    // {
    //     Random random = new Random();
    //     Vector3 diff = main_room_dimension_max - main_room_dimension_min;
    //     // diff = new Vector3(Mathf.Abs(diff.X), Mathf.Abs(diff.Y), Mathf.Abs(diff.Z));
    //     float x_rand = (float)(random.NextDouble())*diff.X + main_room_dimension_min.X;
    //     float y_rand = (float)(random.NextDouble())*diff.Y + main_room_dimension_min.Y;
    //     float z_rand = (float)(random.NextDouble())*diff.Z + main_room_dimension_min.Z;
    //     return new Vector3(x_rand, y_rand, z_rand);
    // }

    // Function to get the closest point on a line to the given point
    public Vector3 GetClosestPointOnLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        // Get the line's direction vector
        Vector3 lineDir = lineEnd - lineStart;

        // Normalize the line direction
        lineDir = lineDir.Normalized();

        // Vector from line start to the point
        Vector3 startToPoint = point - lineStart;

        // Project startToPoint onto the line direction
        float projectionLength = startToPoint.Dot(lineDir);

        // The closest point on the line
        Vector3 closestPoint = lineStart + lineDir * projectionLength;

        return closestPoint;
    }

    public List<CaveCapsuleInfo> GetLeaves(List<CaveCapsuleInfo> lcci)
    {
        List<CaveCapsuleInfo> rv = new List<CaveCapsuleInfo>();
        foreach (var cci in lcci)
        {
            if (cci.Children.Count == 0)
            {
                rv.Add(cci);
            }
        }
        return rv;
    }

    public List<CaveCapsuleInfo> CaveDebugPrototypeRRTStar(int numBranches, int[] MainRoomSpawnIntervals, int[] MainRoomOrientedUp)//no radius check, todo impl lacunarity, etc
    {
        if (RadialLacunarityRange.Y <= height_range.X * 2 / 3)
        {
            GD.Print("LacunarityRadius Max must have a value less than Max of HeightRange 2/3");
        }
        List<CaveCapsuleInfo> Lcci = new List<CaveCapsuleInfo>();
        CaveCapsuleInfo rootCapsuleInfo = new CaveCapsuleInfo(0, RootPosition, RootPosition + Vector3.Down, 1f, 1, 1, null);
        Lcci.Add(rootCapsuleInfo);

        Random random = new Random();
        // branch_depth_meander_range?
        //branch
        //todo add cost for more leaf branching?
        int branch = 0;
        int mainRoomCounter = 0;
        int lastMainRoom = 0;//todo replace this with some data defined ruleset on spawn ordering? or just add rules on spawning depth limits
        while (branch < numBranches)
        {
            GD.Print("on branch: ", branch);


            // int spawnBranch = random.Next(lastMainRoom, Lcci.Count);
            int spawnBranch = branch;
            if (Branching)
            {
                spawnBranch = random.Next(0, Lcci.Count - 1);
            }
            if (anastomosing)
            {
                var leaves = GetLeaves(Lcci);
                spawnBranch = leaves[random.Next(0, leaves.Count - 1)].branchId;
            }
            float x_rand = (float)(random.NextDouble()) * RangeLength(x_range) + x_range.X;
            float y_rand = (float)(random.NextDouble()) * RangeLength(y_range) + y_range.X;
            float z_rand = (float)(random.NextDouble()) * RangeLength(z_range) + z_range.X;

            // float r_rand = .1f;
            // float h_rand = .5f;

            Vector3 v_rand = new Vector3(x_rand, y_rand, z_rand);

            float r_rand = (float)(random.NextDouble()) * RangeLength(radius_range) + radius_range.X;
            float h_rand = (float)(random.NextDouble()) * RangeLength(height_range) + height_range.X;

            // CaveCapsuleInfo BranchCCI = Nearest(v_rand, Lcci);//todo add a cost function for branching? conditional switch 
            GD.Print("Lcci.Count", Lcci.Count);
            GD.Print("branch", branch);
            CaveCapsuleInfo BranchCCI = Lcci[spawnBranch];

            Vector3 dirToVRand = (v_rand - BranchCCI.EndPosition).Normalized();
            bool isMainRoom = false;
            // GD.Print()
            // GD.Print("Nearest CaveCapsule: ", BranchCCI.EndPosition);

            bool converge = anastomosing ? LacunarityCurve.Sample((float)(random.NextDouble())) > Mathf.Pow(LacunarityDecay, BranchCCI.Depth + branch) : false;
            GD.Print("converge: ", converge);
            Vector3 startPosition = BranchCCI.EndPosition;
            Vector3 endPosition = BranchCCI.EndPosition;//placeholder
            // GD.Print("mainRoomCounter", mainRoomCounter);
            // GD.Print("MainRoomSpawnIntervals", MainRoomSpawnIntervals);

            //calculate startPosition and endPosition for the case of prev CCI being MainRoom or normalCCI
            if (MainRoomSpawnIntervals != null &&
                mainRoomCounter < MainRoomSpawnIntervals.Length &&
                MainRoomSpawnIntervals[mainRoomCounter] == branch)
            {
                GD.Print("Generating Main Room");
                // Vector3 roomDims = GenerateMainRoomDimensions();
                float length = ((float)random.NextDouble()) * RangeLength(main_room_length_range) + main_room_length_range.X;
                float radius = ((float)random.NextDouble()) * RangeLength(main_room_radius_range) + main_room_radius_range.X;

                h_rand = length;
                r_rand = radius;

                Vector3 MainRoomDir = MainRoomOrientedUp[mainRoomCounter] > 0 ? Vector3.Up * y_rand : new Vector3(x_rand, 0, z_rand);
                MainRoomDir = MainRoomDir.Normalized() * length;
                Vector3 radiusOffset = MainRoomDir.Normalized() * radius;
                startPosition = BranchCCI.EndPosition + radiusOffset;
                endPosition = startPosition + MainRoomDir;
                // CaveCapsuleInfo mainRoomInfo = new CaveCapsuleInfo(startPosition, endPosition, radius, 1, 
                //     BranchCCI.BranchDepth+1, BranchCCI, true);
                // Lcci.Add(mainRoomInfo);
                lastMainRoom = branch;
                isMainRoom = true;
            }
            else if (BranchCCI.isMainRoom)
            {
                float main_room_radius_rand = (float)(random.NextDouble()) * RangeLength(main_room_radius_range) + main_room_radius_range.X;
                if (BranchCCI.StartPosition.Y == BranchCCI.EndPosition.Y)//check orientation
                {
                    GD.Print("Sideways Main Cave Orientation found..");
                    Vector3 BackwardsDir = BranchCCI.EndPosition - BranchCCI.StartPosition;
                    Vector3 ForwardsDir = BranchCCI.StartPosition - BranchCCI.EndPosition;
                    Vector3 DirFromStart = v_rand - BranchCCI.StartPosition;
                    Vector3 DirFromEnd = v_rand - BranchCCI.EndPosition;
                    if (DirFromStart.Length() <= BranchCCI.Radius && BackwardsDir.AngleTo(DirFromStart) >= Mathf.Pi / 2)
                    {
                        GD.Print("Closest to Start");
                        startPosition = (v_rand - BranchCCI.StartPosition).Normalized() * main_room_radius_rand;
                        endPosition = startPosition + DirFromStart.Normalized() * h_rand;
                    }
                    else if (DirFromEnd.Length() <= BranchCCI.Radius && ForwardsDir.AngleTo(DirFromEnd) >= Mathf.Pi / 2)
                    {
                        GD.Print("Closest to End");
                        startPosition = (v_rand - BranchCCI.EndPosition).Normalized() * main_room_radius_rand;
                        endPosition = startPosition + DirFromEnd.Normalized() * h_rand;
                    }
                    else
                    {
                        GD.Print("Closest to Center");
                        Vector3 cpol = GetClosestPointOnLine(v_rand, BranchCCI.StartPosition, BranchCCI.EndPosition);
                        Vector3 cpolDir = (v_rand - cpol).Normalized();
                        startPosition = cpolDir * main_room_radius_rand;
                        endPosition = startPosition + cpolDir * r_rand;
                    }
                }
                else
                {
                    GD.Print("Vertical Main Cave Orientation found..");
                    startPosition = BranchCCI.EndPosition + dirToVRand.Slide(Vector3.Up).Normalized() * BranchCCI.Radius;
                    GD.Print("dirToVRand.Slide(Vector3.Up)", dirToVRand.Slide(Vector3.Up));
                    endPosition = startPosition + dirToVRand * h_rand;

                }
            }
            else
            {
                endPosition += h_rand * dirToVRand;
            }


            //startPosition = BranchCCI.EndPosition can work with MainRoom prevCapsule if mesh intersection is figured out..
            int _BranchDepth = BranchCCI.Children.Count > 0 ? 1 : BranchCCI.BranchDepth + 1;
            CaveCapsuleInfo nextCCI = new CaveCapsuleInfo(branch, startPosition, endPosition, r_rand, BranchCCI.Depth + 1, _BranchDepth, BranchCCI, isMainRoom);


            CaveCapsuleInfo closestCCI = Nearest(endPosition, Lcci);
            Vector3 vToClosest = closestCCI.EndPosition - endPosition;
            Vector3 nextCCIDir = nextCCI.EndPosition - nextCCI.StartPosition;

            bool isValidBranch = false;
            if (!converge && !InRange(RadialLacunarityRange, (closestCCI.EndPosition - endPosition).Length()))
            {
                GD.Print("opposing force rules of lacunarity radial bounds met, valid branch");
                isValidBranch = true;
            }
            if (converge && InRange(AngularLacunarityRange, nextCCIDir.AngleTo(vToClosest)))
            {
                GD.Print("attracting force angular rules met, valid branch");
                isValidBranch = true;
                if (InRange(RadialLacunarityRange, (closestCCI.EndPosition - endPosition).Length()))
                {

                }
            }
            if (isValidBranch)
            {
                GD.Print("Added to Branch");
                Lcci.Add(nextCCI);
                nextCCI.AddSelfToParentChildren();
                branch++;
                if (nextCCI.isMainRoom)
                { mainRoomCounter++; }
                UpdateCaveCapsuleInfosInRange(nextCCI, Lcci);
            }
            if (nextCCI.isMainRoom && BranchCCI.isMainRoom)
            {
                GD.Print("WARNING: edge case with h_rand");
            }

        }
        return Lcci;
    }

    public override void _Ready()
    {
        base._Ready();
        x_range = x_range * rangeMultiplier;
        y_range = y_range * rangeMultiplier;
        z_range = z_range * rangeMultiplier;
        AngularLacunarityRange = new Vector2(AngularLacunarityRange.X * Mathf.Pi, AngularLacunarityRange.Y * Mathf.Pi);
        _Lcci = CaveDebugPrototypeRRTStar(numBranches, MainRoomSpawnIntervals, MainRoomOrientedUp);
    }
    public override void _Process(double delta)
    {
        base._Process(delta);
        // GD.Print("Starting Capsule Draws:");
        foreach (CaveCapsuleInfo cci in _Lcci)
        {
            // GD.Print("cci.StartPosition: ", cci.StartPosition, "\ncci.EndPosition: ", cci.EndPosition);
            DrawCapsule(cci.StartPosition, cci.EndPosition, cci.Radius);
            if(cci.isMainRoom)
            {
                cylinderMeshGenerator.CreateCylinderMesh(cci);
            }
        }
    }

    public void DrawCapsule(Vector3 StartPosition, Vector3 EndPosition, float radius)
    {
        DebugDraw3D.DrawSphere(StartPosition, radius, Colors.Yellow);
        DebugDraw3D.DrawCylinderAb(StartPosition, EndPosition, radius, Colors.Yellow);
        DebugDraw3D.DrawSphere(EndPosition, radius, Colors.Yellow);
        // DebugDraw3D.DrawLine(StartPosition, EndPosition, Colors.Green);
        DebugDraw3D.DrawArrow(StartPosition, EndPosition, Colors.Green, .1f);
    }
}