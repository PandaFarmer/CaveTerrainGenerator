using Godot;
using System.Collections.Generic;

public class CaveCapsuleInfo
    {
        public int branchId;
        public Vector3 StartPosition;
        public Vector3 EndPosition;
        public float Radius;
        public float Height;
        public int Depth;
        public int BranchDepth;
        public bool isMainRoom;
        public List<CaveCapsuleInfo> Parents;
        public List<CaveCapsuleInfo> Children;
        public CaveCapsuleInfo(int branchId, Vector3 StartPosition, Vector3 EndPosition, float Radius, int Depth, int BranchDepth, CaveCapsuleInfo Parent, bool isMainRoom = false)
        {
            this. branchId = branchId;
            this.StartPosition = StartPosition;
            this.EndPosition = EndPosition;
            this.Radius = Radius;
            this.Depth = Depth;
            this.isMainRoom = isMainRoom;
            Parents = new List<CaveCapsuleInfo>();

            Height = (StartPosition - EndPosition).Length();
            // if(Parent != null)
            // {
            //     Parent.Children.Add(this);
            // }
            Children = new List<CaveCapsuleInfo>();
        }

        public void AddSelfToParentChildren()
        {
            foreach(var Parent in Parents)
            {
                if(!Parent.Children.Contains(this))
                {
                    Parent.Children.Add(this);
                }
            }
        }
    }