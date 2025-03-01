using Godot;
using System.Collections.Generic;

public partial class Quiltmaker : Node3D
{
    [Export]
    int surfaceCountMin = 0;
    int surfaceCountMax = 15;
    public int GetSurfaceCount(Mesh mesh)
    {
        if (mesh is ArrayMesh arrayMesh)
        {
            return arrayMesh.GetSurfaceCount();
        }
        return 0;
    }


    public List<StaticBody3D> SplitIntoPatchesStaticBodies(MeshInstance3D mid3)
    {
        List<StaticBody3D> sb3L = new List<StaticBody3D>();

        return sb3L;
    }

    public List<MeshInstance3D> SplitIntoPatchesMeshInstances(MeshInstance3D mid3)
    {
        List<MeshInstance3D> mid3L = new List<MeshInstance3D>();

        return mid3L;
    }

    private List<Mesh> SplitIntoPatchesMeshes(MeshInstance3D mid3)
    {
        List<Mesh> meshL = new List<Mesh>();

        return meshL;
    }

    private Vector3 AverageOfVectors(List<Vector3> vectors)
    {
        Vector3 net = Vector3.Zero;
        foreach(var v in vectors)
        {
            net+=v;
        }
        return net/vectors.Count;
    }

    public struct PatchChunkCubeInfo
    {
        Vector3 Dimensions;
        Vector3 Position;
        Vector3 Normal;

        public PatchChunkCubeInfo(Vector3 Dimensions, Vector3 Position, Vector3 Normal)
        {
            this.Dimensions = Dimensions;
            this.Position = Position;
            this.Normal = Normal;
        }
    }
}