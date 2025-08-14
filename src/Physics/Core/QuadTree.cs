using System.Runtime.InteropServices;
using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

/// <summary>
/// Manages the QuadTree spatial partitioning structure for the simulation.
/// </summary>
/// <remarks>
/// <see cref="Reset"/> must be called at the start of EVERY timestep, even for the very first one!
/// <br/>
/// The state machine flow is: <c>Reset()</c> -> <c>Insert()</c> -> <c>Evaluate()</c> -> <c>Calc...()</c>
/// </remarks>
internal class QuadTree
{
    #region Public Interface

    /// <summary>
    /// Insert a celestial body into the QuadTree.
    /// </summary>
    /// <param name="body">The body to insert.</param>
    /// <exception cref="InvalidOperationException">If the tree has already been evaluated.</exception>
    /// <exception cref="ArgumentException">If the body is outside the tree's boundary.</exception>
    public void InsertBody(ICelestialBody body)
    {
        ref var root = ref Root;
        if (!root.Bounds.Contains(body)) throw new ArgumentException("The body is outside the tree's boundary.", nameof(body));
        if (root.IsEvaluated) throw new InvalidOperationException("The tree has already been evaluated.");
        root.Insert(body);
    }

    /// <summary>
    /// Evaluates and locks the tree, which must be reset before it can be modified again.
    /// Any attempts to modify the tree before then will throw an exception.
    /// </summary>
    public void Evaluate()
    {
        ref var root = ref Root;
        if (!root.IsEvaluated) root.Evaluate();
    }

    /// <summary>
    /// Calculates the acceleration vector a given body experiences.
    /// </summary>
    /// <param name="body">The celestial body being accelerated.</param>
    /// <param name="calc">The calculator to use for the acceleration calculation.</param>
    public Vector2D CalcAcceleration(ICelestialBody body, ICalculator calc)
    {
        ref var root = ref Root;
        if (!root.IsEvaluated) throw new InvalidOperationException("The tree must be evaluated first.");
        return root.CalcAcceleration(body, calc);
    }

    /// <summary>
    /// Resets and prepares the tree for a new timestep. Must be called before first insertion in every timestep!
    /// </summary>
    /// <param name="minX">West-Boundary. The minimum X position the tree should be capable of accepting.</param>
    /// <param name="minY">South-Boundary. The minimum Y position the tree should be capable of accepting.</param>
    /// <param name="maxX">East-Boundary. The maximum X position the tree should be capable of accepting.</param>
    /// <param name="maxY">North-Boundary. The maximum Y position the tree should be capable of accepting.</param>
    /// <param name="expectedBodies">The number of bodies in this simulation step.</param>
    public void Reset(double minX, double minY, double maxX, double maxY, int expectedBodies = 64)
    {
        if (minX >= maxX || minY >= maxY) throw new ArgumentException("Invalid boundary dimensions.");
        if (expectedBodies <= 0) throw new ArgumentException("Expected bodies must be positive.", nameof(expectedBodies));

        // Reserve extra capacity to avoid list resizing while holding refs.
        _nodes.EnsureCapacity(expectedBodies * 4 + 16);

        // Don't deallocate the lists but just mark all as available.
        // Iterate in reverse so the first available index on the stack is 0.
        _freeNodeIndices.Clear();
        for (int i = _nodes.Count - 1; i >= 0; i--) _freeNodeIndices.Push(i);

        _freeCrowdedBodyListIndices.Clear();
        for (int i = _crowdedBodyLists.Count - 1; i >= 0; i--)
        {
            _crowdedBodyLists[i].Clear();   // clear individual lists to avoid holding onto references 
            _freeCrowdedBodyListIndices.Push(i);
        }

        AABB rootBounds = CreateRootBounds(minX, minY, maxX, maxY);

        // Set root node again. Since the indices have just been cleared, this will set the root node to index 0.
        AllocateNode(rootBounds);
    }

    #endregion

    private const double PADDING_MULT = 0.01;
    private const double PADDING_FLAT = 1e-10;

    private ref Node Root => ref GetNodeRef(0);  // Root node is always index 0.

    private readonly List<Node> _nodes = new(1024); // Pre-allocate capacity
    private readonly Stack<int> _freeNodeIndices = new(256);

    private readonly List<List<ICelestialBody>> _crowdedBodyLists = [];  // Don't pre-allocate as its unlikely to ever be used
    private readonly Stack<int> _freeCrowdedBodyListIndices = new();

    private ref Node GetNodeRef(int nodeIndex) => ref CollectionsMarshal.AsSpan(_nodes)[nodeIndex];
    private ref readonly Node GetNodeRef_RO(int nodeIndex) => ref CollectionsMarshal.AsSpan(_nodes)[nodeIndex];


    private int AllocateNode(AABB bounds, int depth = 0)
    {
        // Re-initialize the recycled node
        if (_freeNodeIndices.Count > 0)
        {
            int index = _freeNodeIndices.Pop();
            _nodes[index] = new Node(this, bounds, depth);
            return index;
        }

        // or add new one
        else
        {
            int index = _nodes.Count;
            _nodes.Add(new Node(this, bounds, depth));
            return index;
        }
    }

    private int AllocateCrowdedBodyList()
    {
        // Reuse a recycled list
        if (_freeCrowdedBodyListIndices.Count > 0)
        {
            // Has already been cleared in the Reset() call
            return _freeCrowdedBodyListIndices.Pop();
        }

        // or add new one
        else
        {
            int index = _crowdedBodyLists.Count;
            _crowdedBodyLists.Add([]);
            return index;
        }
    }

    private static AABB CreateRootBounds(double minX, double minY, double maxX, double maxY)
    {
        double width = maxX - minX;
        double height = maxY - minY;
        double halfWidth = width / 2.0;
        double halfHeight = height / 2.0;

        // Combination of relative and absolute padding to ensure bodies
        // on the exclusive max boundary are inside.
        double padding = Math.Max(width, height) * PADDING_MULT + PADDING_FLAT;
        Vector2D center = new(minX + halfWidth, minY + halfHeight);
        Vector2D halfDimension = new(halfWidth + padding, halfHeight + padding);

        return new(center, halfDimension);
    }

    /// <summary>
    /// Mutable struct to be used via QuadTree's <c>ref</c> returns to
    /// improve locality and reduce GC pressure.
    /// </summary>
    private struct Node
    {
        internal Node(QuadTree tree, AABB bounds, int depth)
        {
            Tree = tree;
            Bounds = bounds;
            _depth = depth;
            IsCrowded = _depth >= MAX_DEPTH;
            if (IsCrowded) _crowdedBodiesIdx = Tree.AllocateCrowdedBodyList();

            Bounds_MaxDimension_sq = Bounds.MaxDimension * Bounds.MaxDimension;
        }

        private const int MAX_DEPTH = 32;

        public readonly QuadTree Tree;
        public readonly AABB Bounds;
        public readonly bool IsCrowded;
        private readonly int _depth;
        private double Bounds_MaxDimension_sq { get; init; }

        private ICelestialBody? _body;

        private readonly int _crowdedBodiesIdx = -1;
        private readonly List<ICelestialBody> CrowdedBodies
        {
            get
            {
                if (!IsCrowded) throw new InvalidOperationException("Node is not crowded and thus cannot have crowded bodies.");
                return Tree._crowdedBodyLists[_crowdedBodiesIdx];
            }
        }

        #region Evaluated Properties

        public bool IsEvaluated = false;
        public double Mass { get; private set; } = 0;
        public Vector2D CenterOfMass { get; private set; } = Vector2D.Zero;
        public readonly Vector2D WeightedPosition => CenterOfMass * Mass;   // Can be directly calculated as it's usually only used once per node.

        #endregion


        #region Child Node Properties

        public readonly bool IsLeaf => ChildNodeIdx_NW == -1;

        private int ChildNodeIdx_NW = -1;
        private int ChildNodeIdx_NE = -1;
        private int ChildNodeIdx_SW = -1;
        private int ChildNodeIdx_SE = -1;

        private ref Node NW_Child => ref _GetChildRefWorker(ChildNodeIdx_NW);
        private ref Node NE_Child => ref _GetChildRefWorker(ChildNodeIdx_NE);
        private ref Node SW_Child => ref _GetChildRefWorker(ChildNodeIdx_SW);
        private ref Node SE_Child => ref _GetChildRefWorker(ChildNodeIdx_SE);

        private readonly ref readonly Node NW_Child_RO => ref _GetChildRefWorker_RO(ChildNodeIdx_NW);
        private readonly ref readonly Node NE_Child_RO => ref _GetChildRefWorker_RO(ChildNodeIdx_NE);
        private readonly ref readonly Node SW_Child_RO => ref _GetChildRefWorker_RO(ChildNodeIdx_SW);
        private readonly ref readonly Node SE_Child_RO => ref _GetChildRefWorker_RO(ChildNodeIdx_SE);


        private ref Node _GetChildRefWorker(int childIdx)
        {
            if (IsLeaf) throw new InvalidOperationException("Node is a leaf and does not have any child nodes.");
            return ref Tree.GetNodeRef(childIdx);
        }

        private readonly ref readonly Node _GetChildRefWorker_RO(int childIdx)
        {
            if (IsLeaf) throw new InvalidOperationException("Node is a leaf and does not have any child nodes.");
            return ref Tree.GetNodeRef_RO(childIdx);
        }

        #endregion


        #region Insertion

        public void Insert(ICelestialBody newBody)
        {
            if (IsCrowded)
            {
                // List was allocated in the constructor
                CrowdedBodies.Add(newBody);
                return;
            }

            if (IsLeaf)
            {
                // Simply insert if possible
                if (_body == null)
                {
                    _body = newBody;
                    return;
                }

                // Otherwise subdivide and pass the current body to a child
                Subdivide();
                DistributeToChild(_body);
                _body = null;
            }

            DistributeToChild(newBody);
        }

        private void Subdivide()
        {
            if (!IsLeaf) throw new InvalidOperationException("Node is already an internal node and cannot be further subdivided.");

            var center = Bounds.Center;
            var newHalfDim = Bounds.HalfDimension * 0.5;

            var nwCenter = center + new Vector2D(-newHalfDim.X, newHalfDim.Y);
            var neCenter = center + new Vector2D(newHalfDim.X, newHalfDim.Y);
            var swCenter = center + new Vector2D(-newHalfDim.X, -newHalfDim.Y);
            var seCenter = center + new Vector2D(newHalfDim.X, -newHalfDim.Y);

            ChildNodeIdx_NW = Tree.AllocateNode(new AABB(nwCenter, newHalfDim), _depth + 1);
            ChildNodeIdx_NE = Tree.AllocateNode(new AABB(neCenter, newHalfDim), _depth + 1);
            ChildNodeIdx_SW = Tree.AllocateNode(new AABB(swCenter, newHalfDim), _depth + 1);
            ChildNodeIdx_SE = Tree.AllocateNode(new AABB(seCenter, newHalfDim), _depth + 1);
        }

        private void DistributeToChild(ICelestialBody body)
        {
            var bodySouth = body.Position.Y < Bounds.Center.Y;
            var bodyWest = body.Position.X < Bounds.Center.X;

            // Can insert directly since we know it is the right quadrant.
            if (bodySouth)
            {
                if (bodyWest) SW_Child.Insert(body);
                else SE_Child.Insert(body);
            }
            else
            {
                if (bodyWest) NW_Child.Insert(body);
                else NE_Child.Insert(body);
            }
        }

        #endregion


        #region Mass Distribution

        public void Evaluate()
        {
            Mass = 0;
            CenterOfMass = Vector2D.Zero;

            // Case 1: Crowded
            if (IsCrowded)
            {
                Vector2D weightedPositionSum = Vector2D.Zero;
                foreach (var body in CrowdedBodies)
                {
                    Mass += body.Mass;
                    weightedPositionSum += body.Position * body.Mass;
                }

                if (Mass != 0) CenterOfMass = weightedPositionSum / Mass;
            }

            // Case 2: External
            else if (IsLeaf && _body != null)
            {
                Mass = _body.Mass;
                if (Mass != 0) CenterOfMass = _body.Position;
            }

            // Case 3: Internal
            else
            {
                // Recursively evaluate child nodes.
                NW_Child.Evaluate();
                NE_Child.Evaluate();
                SW_Child.Evaluate();
                SE_Child.Evaluate();

                Mass += NW_Child.Mass + NE_Child.Mass + SW_Child.Mass + SE_Child.Mass;
                if (Mass != 0) CenterOfMass = (
                    NW_Child.WeightedPosition
                    + NE_Child.WeightedPosition
                    + SW_Child.WeightedPosition
                    + SE_Child.WeightedPosition
                    ) / Mass;
            }

            IsEvaluated = true;
        }

        #endregion


        #region Acceleration

        public readonly Vector2D CalcAcceleration(ICelestialBody body, ICalculator calc)
        {
            if (Mass == 0) return Vector2D.Zero;
            if (IsLeaf && _body?.Id == body.Id) return Vector2D.Zero;   // Ensure to never include self.

            double d_sq_softened = calc.DistanceSquaredSoftened(body.Position, CenterOfMass);
            // s / d < θ
            bool isFar = Bounds_MaxDimension_sq / d_sq_softened < calc.ThetaSquared;

            // Case A: Node is far away => simply treat the node itself as a single point mass
            if (isFar) return calc.Acceleration(body.Position, CenterOfMass, Mass, d_sq_softened);

            // Case B: Node is a Leaf
            if (IsLeaf)
            {
                // Case B_1: Leaf Node is crowded => Iterate through the crowded bodies and accumulate the results
                if (IsCrowded)
                {
                    Vector2D finalAcc = Vector2D.Zero;
                    foreach (var _crowdedBody in CrowdedBodies)
                    {
                        if (_crowdedBody.Id != body.Id) finalAcc += calc.Acceleration(body.Position, _crowdedBody.Position, _crowdedBody.Mass);
                    }
                    return finalAcc;
                }

                // Case B_2: Leaf Node is not crowded => use the single body
                // Distance to node center of mass is same as distance to body so we can pass that along to avoid expensive recalculation.
                if (_body != null) return calc.Acceleration(body.Position, _body.Position, _body.Mass, d_sq_softened);
            }

            // Case C: Node is Internal => Recurse into children and accumulate the results
            return NW_Child_RO.CalcAcceleration(body, calc)
                + NE_Child_RO.CalcAcceleration(body, calc)
                + SW_Child_RO.CalcAcceleration(body, calc)
                + SE_Child_RO.CalcAcceleration(body, calc);
        }

        #endregion
    }
}
