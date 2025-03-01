using Godot;
using System;

public partial class CameraController : Camera3D
{
    // [Export]
    // public CharacterBody3D player;
    // [Export]
    // public CharacterBody3D player;
    [Export]
    public bool InvertVertical = false;
    [Export]
    public bool InvertHorizontal = false;
    [Export]
    public float OrbitSpeed = 0.01f;
    
    [Export]
    public float MinVerticalAngle = -45.0f;
    
    [Export]
    public float MaxVerticalAngle = 45.0f;
    
    [Export]
    public Node3D Target;
    
    [Export]
    public Camera3D Camera;
    
    private bool _isDragging = false;
    private Vector2 _lastMousePosition;

    private float _camera_distance;

    private float MinVerticalRadians;
    private float MaxVerticalRadians;

    float delta_yaw;
    float delta_pitch;
    
    public override void _Ready()
    {
        if (Camera == null)
        {
            GD.PrintErr("Camera reference not set in CameraOrbitController!");
            return;
        }
        
        if (Target == null)
        {
            GD.PrintErr("Target reference not set in CameraOrbitController!");
            return;
        }
        
        // Initialize rotation values based on current transform
        Vector3 currentRotation = RotationDegrees;
        _camera_distance = (Camera.Position - Target.Position).Length();
        MinVerticalRadians = Mathf.DegToRad(MinVerticalAngle);
        MaxVerticalRadians = Mathf.DegToRad(MaxVerticalAngle);
    }
    
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                _isDragging = mouseButton.Pressed;
                if (_isDragging)
                {
                    _lastMousePosition = mouseButton.Position;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            Vector2 mouseDelta = mouseMotion.Position - _lastMousePosition;
            _lastMousePosition = mouseMotion.Position;
            
            // Update rotation values
            float _rotationX = mouseDelta.X * OrbitSpeed;
            float _rotationY = mouseDelta.Y * OrbitSpeed;
            
            // Clamp vertical rotation
            _rotationY = Mathf.Clamp(_rotationY, MinVerticalAngle, MaxVerticalAngle);

            delta_yaw = _rotationX;
            delta_pitch = _rotationY;
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdateCameraPosition(((float)delta));
    }
 
    private void UpdateCameraPosition(float delta)
    {
        Vector3 euler_angles = Transform.Basis.GetEuler();
        GD.Print("euler_angles: ", euler_angles);
        float pitch = euler_angles.X;
        float yaw = euler_angles.Y;
        float roll = euler_angles.Z;
        pitch = Mathf.Clamp(pitch+delta_pitch, MinVerticalRadians, MaxVerticalRadians);
        yaw = yaw + delta_yaw;
        Vector3 eulerXYZ = new Vector3(pitch, yaw, roll);
        Basis newBasis = new Basis(Quaternion.FromEuler(eulerXYZ));
        
        Vector3 newPosition = Target.Position + (_camera_distance*Transform.Basis.Z * delta* OrbitSpeed).Normalized()*_camera_distance;
        Transform = new Transform3D(newBasis, newPosition);
        delta_yaw = 0f;
        delta_pitch = 0f;
    }
}