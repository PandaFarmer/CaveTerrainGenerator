using Godot;
public partial class CaveBiomeInfoHandler : Node3D
{
    [Export]
    public string name;
    [Export]
    public float vertexGranularity;
    [Export]
    public bool isTunnel;
    [Export]
    public bool flatBase;
    [Export]
    public float noise;
    [Export]
    public FastNoiseLite fnl;
}