using Godot;

public partial class MeshDeformer : Node3D
{
    public SurfaceTool surfaceTool;
    public MeshDataTool data;
    public Mesh mesh;
    public ulong meshIdFromCollider;


    public void AssignMeshDataTool(Mesh mesh)
    {
        this.mesh = mesh;
        surfaceTool = new SurfaceTool();
        surfaceTool.CreateFrom(mesh, 0);
        data = new MeshDataTool();
        var arrayPlane = surfaceTool.Commit();
        data.CreateFromSurface(arrayPlane, 0);

        meshIdFromCollider = mesh.GetRid().Id;
    }

    public void IndentVerticesInRadius(MeshInstance3D mid3, Vector3 position, Vector3 normal, float radius)
    {
        if(data == null)
        {
            GD.Print("WARNING: MeshDataTool not instanced for MeshDeformer");
            return;
        }

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, new BoxMesh().GetMeshArrays());
        // var mdt = new MeshDataTool();
        // mdt.CreateFromSurface(mesh, 0);

        //iterate over and deform?
        for(int i = 0; i < data.GetVertexCount(); i++)
        {
            var vertex = data.GetVertex(i);
            if ((position - vertex).Length() > radius)
            {
                data.SetVertex(i, vertex-normal.Normalized()*radius);
            }
        }

        mesh.ClearSurfaces();
        data.CommitToSurface(mesh);
        mid3.Mesh = mesh;
    }
    
    //mesh.clear_surfaces() # Deletes all of the mesh's surfaces.
    //mdt.commit_to_surface(mesh)
    //can you add meshes simply by omiting clear_surfaces before commit_to_surface?
    //delete surface and delete/move vertices
    //or write a c++ plugin
    public void DeleteVerticesInRadius(ArrayMesh arrayMesh, Vector3 position, float radius)
    {
        if(data == null)
        {
            GD.Print("WARNING: MeshDataTool not instanced for MeshDeformer");
        }
        

        //iterate over and deform?
        for(int i = 0; i < data.GetVertexCount(); i++)
        {
            var vertex = data.GetVertex(i);
            if ((position - vertex).Length() > radius)
            {
                data.SetVertex(i, vertex);
            }
        }
    }

    public void AddInterpolatedVerticesAlongRadius()
    {
        if(data == null)
        {
            GD.Print("WARNING: MeshDataTool not instanced for MeshDeformer");
        }
    }
}