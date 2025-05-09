using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Godot;

public partial class CaveTestRRT : Node3D
{

    [Export] float rangeMultiplier = 100f;
    [Export] Vector2 x_range = new Vector2(-1, 1);
    [Export] Vector2 y_range = new Vector2(-1, 0);
    [Export] Vector2 z_range = new Vector2(-1, 1);

    Vector3 average_range_v;
    [Export] Vector2 radius_range = new Vector2(.1f, .2f);
    [Export] Vector2 height_range = new Vector2(3, 6);

    // [Export]
    // Vector3 main_room_dimension_min = new Vector3(5, 5, 5);
    // [Export]
    // Vector3 main_room_dimension_max = new Vector3(10, 10, 10);
    [Export] Vector2 main_room_length_range = new Vector2(2, 4);
    [Export] Vector2 main_room_radius_range = new Vector2(4, 8);

    [Export] float branching_factor = .3f;//probability of branching, could also do some curve with domain pow of depth

    [Export] float depthResetRadius = 2f;//reset depth and snap to branch joint if within a certain distance
    [Export] Vector2 RadialLacunarityRange = new Vector2(0, 2f);//opposing
    [Export] Vector2 AngularLacunarityRange = new Vector2(0, .2f);//Values should be clamped to -1, 1, mul by Mathf.Pi, attraction

    [Export] Curve LacunarityCurve;//defines a pda for opposing/attracting invariants
    [Export] float LacunarityGeometricDecay = .7f;
    [Export] float LacunarityAdditiveDecay = .2f;
    [Export] float LacunarityScaling = 2f;
    [Export] bool LacunarityIsGeometric = false;

    [Export] public int[] MainRoomSpawnIntervals;
    [Export] public int[] MainRoomOrientedUp;

    // [Export]
    // Vector3 Drift = Vector3.Down;
    [Export] bool DrawOn = true;
    [Export] bool SurfaceNetsOn = false;
    [Export] bool Branching;
    [Export] bool anastomosing;
    [Export] int anastomosingLeafLimit = 5;
    [Export] float anastomosingDepthRatio = .1f;//ratio to allow stub leaves to merge
    [Export] int searchLimit = 100;
    [Export] int numBranches = 20;
    [Export] bool localizedAnastomosing = false;
    [Export] Vector3 RootPosition = Vector3.Up * 70f;

    public List<CaveCapsuleInfo> _Lcci;
    private int max_depth;

    // [Export] public CylinderMeshGenerator cylinderMeshGenerator;


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

    public CaveCapsuleInfo NearestByEndPosition(Vector3 v3, List<CaveCapsuleInfo> llv)
    {
        CaveCapsuleInfo NearestByEndPosition = llv[0];

        float distance = Mathf.Inf;
        foreach (var cci in llv)
        {

            float _distance = (cci.EndPosition - v3).Length();
            if (_distance < distance)
            {
                NearestByEndPosition = cci;
                distance = _distance;
            }
        }
        return NearestByEndPosition;
    }

    public CaveCapsuleInfo NearestByStartPosition(Vector3 v3, List<CaveCapsuleInfo> llv)
    {
        CaveCapsuleInfo NearestByStartPosition = llv[0];

        float distance = Mathf.Inf;
        foreach (var cci in llv)
        {

            float _distance = (cci.EndPosition - v3).Length();
            if (_distance < distance)
            {
                NearestByStartPosition = cci;
                distance = _distance;
            }
        }
        return NearestByStartPosition;
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

    public List<CaveCapsuleInfo> GetLeaves(List<CaveCapsuleInfo> _Lcci)
    {
        List<CaveCapsuleInfo> rv = new List<CaveCapsuleInfo>();
        foreach (var cci in _Lcci)
        {
            if (cci.Children.Count == 0)
            {
                rv.Add(cci);
            }
        }
        return rv;
    }

    private Vector3 GenerateVRand()
    {
        Random random = new Random();
        float x_rand = (float)(random.NextDouble()) * RangeLength(x_range) + x_range.X;
        float y_rand = (float)(random.NextDouble()) * RangeLength(y_range) + y_range.X;
        float z_rand = (float)(random.NextDouble()) * RangeLength(z_range) + z_range.X;

        // float r_rand = .1f;
        // float h_rand = .5f;

        Vector3 v_rand = new Vector3(x_rand, y_rand, z_rand);

        return v_rand;
    }

    public static int SelectBucketRandomlyWithBias(List<int> bucketSizes)
    {
        // Step 1: Calculate total weight (inverse of sizes)
        double totalWeight = bucketSizes.Sum(size => 1.0 / size);

        // Step 2: Generate a random value between 0 and totalWeight
        Random rand = new Random();
        double randomValue = rand.NextDouble() * totalWeight;

        // Step 3: Iterate through buckets and select the one that the random value falls into
        double cumulativeWeight = 0.0;

        for (int i = 0; i < bucketSizes.Count; i++)
        {
            // Add the inverse size to cumulative weight
            cumulativeWeight += 1.0 / bucketSizes[i];

            // If the random value falls within this cumulative weight, select the bucket
            if (randomValue <= cumulativeWeight)
            {
                return i; // Return the index of the selected bucket
            }
        }

        // In case something goes wrong, this will never happen with valid input
        return -1;
    }

    private (Vector3 startPosition, Vector3 endPosition) AveragedStartEnd(List<CaveCapsuleInfo> _Lcci)
    {
        Vector3 startPosition = Vector3.Zero;
        Vector3 endPosition = Vector3.Zero;
        if (_Lcci == null)
        {
            GD.Print("Lcci Null, expected value");
        }
        foreach (var cci in _Lcci)
        {
            startPosition += cci.StartPosition;
            endPosition += cci.EndPosition;
        }
        return (startPosition / _Lcci.Count, endPosition / _Lcci.Count);
    }

    public List<int> LeafCCIDepths(List<CaveCapsuleInfo> leafCCis)
    {

        List<int> leafCCIDepths = new List<int>();
        foreach (var leafCCi in leafCCis)
        {
            leafCCIDepths.Add(leafCCi.Depth);
        }
        return leafCCIDepths;
    }

    private float GenerateRandomValueInRange(Vector2 range)
    {
        Random random = new Random();
        return (float)(random.NextDouble()) * RangeLength(range) + range.X;
    }



    private (Vector3 startPosition, Vector3 endPosition) GenerateBaseCCIDims(CaveCapsuleInfo BranchCCI,
    Vector3 v_rand, float h_rand, Vector3 dirToVRand)
    {
        Vector3 startPosition = BranchCCI.EndPosition;//placeholders
        Vector3 endPosition = BranchCCI.EndPosition;//placeholders

        endPosition += h_rand * dirToVRand;

        return (startPosition, endPosition);
    }

    public void GenerateCCIAnastomosing(int numBranches, int[] MainRoomSpawnIntervals,
        int[] MainRoomOrientedUp)//no radius check, todo impl lacunarity, etc
    {
        Random random = new Random();
        int searchCount = 0;
        int branch = 0;
        int spawnBranch = 1;
        bool isMainRoom = false;


        while (branch < numBranches && searchCount < searchLimit)
        {
            List<CaveCapsuleInfo> leafCCis = GetLeaves(_Lcci);
            List<int> leafCCIDepths = LeafCCIDepths(leafCCis);
            int spawnLeafIndex;
            if (leafCCis.Count >= anastomosingLeafLimit)
            {
                spawnLeafIndex = SelectBucketRandomlyWithBias(leafCCIDepths);
                spawnBranch = _Lcci.IndexOf(leafCCis[spawnLeafIndex]);
            }
            else
            {
                spawnBranch = random.Next(0, _Lcci.Count - 1);
            }
            CaveCapsuleInfo BranchCCI = _Lcci[spawnBranch];


            Vector3 v_rand = GenerateVRand();
            float r_rand = GenerateRandomValueInRange(radius_range);
            float h_rand = GenerateRandomValueInRange(height_range);

            Vector3 dirToVRand = (v_rand - BranchCCI.EndPosition).Normalized();
            (Vector3 startPosition, Vector3 endPosition) = GenerateBaseCCIDims(BranchCCI,
                v_rand, h_rand, dirToVRand);

            //startPosition = BranchCCI.EndPosition can work with MainRoom prevCapsule if mesh intersection is figured out..
            int _BranchDepth = BranchCCI.Children.Count > 0 ? 1 : BranchCCI.BranchDepth + 1;
            CaveCapsuleInfo nextCCI = new CaveCapsuleInfo(branch, startPosition, endPosition, r_rand, BranchCCI.Depth + 1,
                _BranchDepth, BranchCCI, isMainRoom);

            //need to diregard valid branch check, check for merge first..
            //
            float alpha = nextCCI.BranchDepth;
            (bool validBranch, CaveCapsuleInfo closestMergeCCI, Vector3 mergeV) = BranchCheck(nextCCI, _Lcci, alpha);
            float compareDist = nextCCI.isMainRoom ? height_range.X + height_range.Y : main_room_length_range.X + main_room_length_range.Y;
            compareDist = 2 / 3 * (compareDist + closestMergeCCI.Radius);

            if (alpha >= 1 && alpha >= 1 && (nextCCI.StartPosition - mergeV).Length() <= compareDist)
            {
                GD.Print("Merged to Branch");
                nextCCI.EndPosition = mergeV;
                nextCCI.BranchDepth = 1;
                closestMergeCCI.BranchDepth = 1;
                closestMergeCCI.Parents.Add(nextCCI);
                nextCCI.Children.Add(closestMergeCCI);
                validBranch = true;
            }
            if (validBranch)
            {
                GD.Print("Added to Branch");
                _Lcci.Add(nextCCI);
                max_depth = nextCCI.Depth > max_depth ? nextCCI.Depth : max_depth;
                nextCCI.AddSelfToParentChildren();
                branch++;
                // if (nextCCI.isMainRoom)
                // { mainRoomCounter++; }
                UpdateCaveCapsuleInfosInRange(nextCCI, _Lcci);
            }
            searchCount++;
            // if (nextCCI.isMainRoom && BranchCCI.isMainRoom)
            // {
            //     GD.Print("WARNING: edge case with h_rand");
            // }
        }
    }

    public void GenerateCCI(int numBranches, int[] MainRoomSpawnIntervals, int[] MainRoomOrientedUp)//no radius check, todo impl lacunarity, etc
    {


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


            // int spawnBranch = random.Next(lastMainRoom, _Lcci.Count);
            int spawnBranch = branch;
            if (Branching)
            {
                spawnBranch = random.Next(0, _Lcci.Count - 1);
            }

            Vector3 v_rand = GenerateVRand();

            float r_rand = (float)(random.NextDouble()) * RangeLength(radius_range) + radius_range.X;
            float h_rand = (float)(random.NextDouble()) * RangeLength(height_range) + height_range.X;

            // CaveCapsuleInfo BranchCCI = NearestByEndPosition(v_rand, _Lcci);//todo add a cost function for branching? conditional switch 
            GD.Print("Lcci.Count", _Lcci.Count);
            GD.Print("branch", branch);
            CaveCapsuleInfo BranchCCI = _Lcci[spawnBranch];

            Vector3 dirToVRand = (v_rand - BranchCCI.EndPosition).Normalized();
            bool isMainRoom = false;
            // GD.Print()
            // GD.Print("NearestByEndPosition CaveCapsule: ", BranchCCI.EndPosition);

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

                Vector3 MainRoomDir = MainRoomOrientedUp[mainRoomCounter] > 0 ? Vector3.Up * v_rand.Y : new Vector3(v_rand.X, 0, v_rand.Z);
                MainRoomDir = MainRoomDir.Normalized() * length;
                Vector3 radiusOffset = MainRoomDir.Normalized() * radius;
                startPosition = BranchCCI.EndPosition + radiusOffset;
                endPosition = startPosition + MainRoomDir;
                // CaveCapsuleInfo mainRoomInfo = new CaveCapsuleInfo(startPosition, endPosition, radius, 1, 
                //     BranchCCI.BranchDepth+1, BranchCCI, true);
                // _Lcci.Add(mainRoomInfo);
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
            CaveCapsuleInfo nextCCI = new CaveCapsuleInfo(branch, startPosition, endPosition, r_rand, BranchCCI.Depth + 1,
                _BranchDepth, BranchCCI, isMainRoom);


            float alpha = nextCCI.BranchDepth;
            (bool validBranch, CaveCapsuleInfo closestMergeCCI, Vector3 mergeV) = BranchCheck(nextCCI, _Lcci, alpha);
            float compareDist = nextCCI.isMainRoom ? height_range.X + height_range.Y : main_room_length_range.X + main_room_length_range.Y;
            compareDist = 2 / 3 * (compareDist + closestMergeCCI.Radius);
            if (validBranch && alpha >= 1 && (nextCCI.StartPosition - mergeV).Length() <= compareDist)
            {
                if (closestMergeCCI.StartPosition == mergeV || closestMergeCCI.EndPosition == mergeV)
                {
                    nextCCI.EndPosition = mergeV;
                    nextCCI.BranchDepth = 1;
                    closestMergeCCI.BranchDepth = 1;
                }
            }

            if (validBranch)
            {
                GD.Print("Added to Branch");
                _Lcci.Add(nextCCI);
                nextCCI.AddSelfToParentChildren();
                branch++;
                if (nextCCI.isMainRoom)
                { mainRoomCounter++; }
                UpdateCaveCapsuleInfosInRange(nextCCI, _Lcci);
            }
            if (nextCCI.isMainRoom && BranchCCI.isMainRoom)
            {
                GD.Print("WARNING: edge case with h_rand");
            }

        }
    }

    // private GpuParticlesAttractorVectorField3D 

    private void AutoSetCCIEndPositionToClosestOnSegment(Vector3 StartPosition, Vector3 EndPosition, CaveCapsuleInfo cci, float Threshold)
    {
        var _ClosestPointOnSegment = ClosestPointOnSegment(StartPosition, EndPosition, cci.EndPosition);
        var _SegmentToSegmentDistance = SegmentToSegmentDistance(StartPosition, EndPosition, cci.StartPosition, cci.EndPosition);
        if (_SegmentToSegmentDistance >= Threshold)
        {
            cci.EndPosition = _ClosestPointOnSegment;
        }
    }

    //returns isValid, isMergeByStart, MergePosition
    private (bool, CaveCapsuleInfo, Vector3) BranchCheck(CaveCapsuleInfo cci, List<CaveCapsuleInfo> ExistingCaveCapsules, float alpha)
    {
        //a is alpha, a = lacunarity
        //ccciJ = closest cci joint
        //compareval = a*(endposition distance to ccciJ) + (1-a)*(angular difference between vector to ccciJ and generated vector)
        //isValid = (compareval > curve defined on a domain)

        var endPosition = cci.EndPosition;
        Vector3 closest_pos = Vector3.Inf;
        float closest_ssd = Mathf.Inf;
        CaveCapsuleInfo closestCCI = null;

        // bool automerge = false;

        if (anastomosing)
        {

            foreach (var ExistingCCI in ExistingCaveCapsules)
            {
                var _ClosestPointOnSegment = ClosestPointOnSegment(ExistingCCI.StartPosition, ExistingCCI.EndPosition, endPosition);
                var _SegmentToSegmentDistance = SegmentToSegmentDistance(ExistingCCI.StartPosition, ExistingCCI.EndPosition,
                    cci.StartPosition, cci.EndPosition);
                if (_SegmentToSegmentDistance <= depthResetRadius && _SegmentToSegmentDistance < closest_ssd)
                {
                    closest_pos = _ClosestPointOnSegment;
                    closest_ssd = _SegmentToSegmentDistance;
                    closestCCI = ExistingCCI;
                }
            }

            if (closestCCI != null)
            {
                AutoSetCCIEndPositionToClosestOnSegment(closestCCI.StartPosition, closestCCI.EndPosition, cci, depthResetRadius);
                cci.EndPosition = closest_pos;
            }
        }
        //todo need to allow for low alpha branching when depth relative to maxdepth is small?
        //random merge? or merge to localized/global average?


        CaveCapsuleInfo startClosest = NearestByStartPosition(endPosition, ExistingCaveCapsules);
        CaveCapsuleInfo endClosest = NearestByEndPosition(endPosition, ExistingCaveCapsules);

        float startClosestDist = (endPosition - startClosest.StartPosition).Length();
        float endClosestDist = (endPosition - endClosest.EndPosition).Length();

        bool isMergeByStart = startClosestDist < endClosestDist;


        var closestV = isMergeByStart ? startClosest.StartPosition : endClosest.EndPosition;
        Vector3 cciV = cci.EndPosition - cci.StartPosition;
        Vector3 dirToClosestV = closestV - cci.StartPosition;
        float compare_val = alpha * (endPosition - closestV).Length() + (1 - alpha) * cciV.AngleTo(dirToClosestV);


        return (compare_val > LacunarityCurve.Sample(alpha), isMergeByStart ? startClosest : endClosest, closestV);
    }

    private Vector3 ClosestPointOnSegment(Vector3 A, Vector3 B, Vector3 P)
    {
        var AB = B - A;
        var AP = P - A;

        var AB_dot_AB = AB.Dot(AB);
        var AB_dot_AP = AB.Dot(AP);

        var t = AB_dot_AP / AB_dot_AB;

        t = Mathf.Max(0, Mathf.Min(1, t));

        var C = A + t * AB;
        return C;
    }

    // Method to calculate the minimum distance between two line segments
    private float SegmentToSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
    {
        // Vector calculations
        Vector3 p1p2 = p2 - p1;  // Vector representing the first line segment
        Vector3 q1q2 = q2 - q1;  // Vector representing the second line segment
        Vector3 p1q1 = q1 - p1;  // Vector from p1 to q1

        // Scalar products
        float dotP1P2P1Q1 = p1p2.Dot(p1q1);
        float dotP1P2P1P2 = p1p2.LengthSquared();
        float dotQ1Q2Q1Q1 = q1q2.LengthSquared();
        float dotQ1Q2P1Q1 = q1q2.Dot(p1q1);

        // Parameter for the closest point on the first segment (t)
        float t = Mathf.Clamp(dotP1P2P1Q1 / dotP1P2P1P2, 0f, 1f);
        // Parameter for the closest point on the second segment (s)
        float s = Mathf.Clamp(dotQ1Q2Q1Q1 - dotQ1Q2P1Q1 / dotQ1Q2Q1Q1, 0f, 1f);

        // Closest points on both segments
        Vector3 closestP1 = p1 + t * p1p2;
        Vector3 closestQ1 = q1 + s * q1q2;

        // Calculate the distance between the closest points
        float distance = closestP1.DistanceTo(closestQ1);
        return distance;
    }

    private GodotObject ArbitraryCylinderSurfaceNetGenerator;
    private bool hasGeneratedSurfaceNets;

    public override void _Ready()
    {
        base._Ready();
        hasGeneratedSurfaceNets = false;

        x_range = x_range * rangeMultiplier;
        y_range = y_range * rangeMultiplier;
        z_range = z_range * rangeMultiplier;

        average_range_v = new Vector3((x_range.X + x_range.Y) / 2, (y_range.X + y_range.Y) / 2, (z_range.X + z_range.Y) / 2);
        average_range_v = average_range_v.Normalized(); //used to shear/offset anastomosing merge direction
        AngularLacunarityRange = new Vector2(AngularLacunarityRange.X * Mathf.Pi, AngularLacunarityRange.Y * Mathf.Pi);

        if (RadialLacunarityRange.Y <= height_range.X * 2 / 3)
        {
            GD.Print("LacunarityRadius Max must have a value less than Max of HeightRange 2/3");
        }
        _Lcci = new List<CaveCapsuleInfo>();
        CaveCapsuleInfo rootCapsuleInfo = new CaveCapsuleInfo(0, RootPosition, RootPosition + Vector3.Down, 1f, 1, 1, null);
        _Lcci.Add(rootCapsuleInfo);

        if (anastomosing)
        {
            GenerateCCIAnastomosing(numBranches, MainRoomSpawnIntervals, MainRoomOrientedUp);
        }
        else
        {
            GenerateCCI(numBranches, MainRoomSpawnIntervals, MainRoomOrientedUp);
        }

        // GeometrySampler = (MeshInstance3D)GetTree().Root.GetNode("GeometrySampler");
    }

    // private void TestClosestPointOnSegment()
    // {

    // }
    
    public override void _Process(double delta)
    {

        base._Process(delta);
        // var myGDScript = GD.Load<GDScript>("res://MeshDeform/DrawCylinder.gd");
        // var ArbitraryCylinderSurfaceNetGenerator = (GodotObject)myGDScript.New(); // This is a GodotObject.

        // if (SurfaceNetsOn && !hasGeneratedSurfaceNets)
        // {
        //     foreach (CaveCapsuleInfo cci in _Lcci)
        //     {
        //         ArbitraryCylinderSurfaceNetGenerator.Set("CENTER", RootPosition);
        //         ArbitraryCylinderSurfaceNetGenerator.Set("RADIUS", cci.Radius);
        //         ArbitraryCylinderSurfaceNetGenerator.Set("HEIGHT", cci.Height);
        //         ArbitraryCylinderSurfaceNetGenerator.Set("a", cci.StartPosition);
        //         ArbitraryCylinderSurfaceNetGenerator.Set("b", cci.EndPosition);
        //         ArbitraryCylinderSurfaceNetGenerator.Call("draw");
        //     }
        //     hasGeneratedSurfaceNets = true;
        // }

        // GD.Print("Starting Capsule Draws:");
        foreach (CaveCapsuleInfo cci in _Lcci)
        {
            // GD.Print("cci.StartPosition: ", cci.StartPosition, "\ncci.EndPosition: ", cci.EndPosition);

            if (DrawOn)
            {
                DrawCapsule(cci.StartPosition, cci.EndPosition, cci.Radius);
            }


            // if(cci.isMainRoom)
            // {
            //     cylinderMeshGenerator.attachmentNode = (Node3D)GetTree().CurrentScene;
            //     cylinderMeshGenerator.CreateCylinderMesh(cci);
            // }
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