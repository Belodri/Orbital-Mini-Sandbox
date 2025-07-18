using System.Diagnostics;
using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

internal interface IQuadTreeNode
{
    /// <summary>
    /// Tracks how many bodies are in this node and all of its children.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Tracks if the node has been evaluated and is locked from further modification.
    /// </summary>
    bool IsEvaluated { get; }

    /// <summary>
    /// Iterator to access the bodies contained directly within this node.
    /// If the node is an internal node, the enumeration is empty. 
    /// </summary>
    IEnumerable<ICelestialBody> Bodies { get; }

    /// <summary>
    /// Iterator to safely access the existing child nodes.
    /// </summary>
    IEnumerable<IQuadTreeNode> Children { get; }

    /// <summary>
    /// The rectangular boundary that this node represents in the simulation space.
    /// </summary>
    AABB Boundary { get; }

    /// <summary>
    /// A flag indicating whether this node is a external or an internal node.
    /// </summary>
    bool IsExternal { get; }

    /// <summary>
    /// The combined total mass of the node (either its body or of all of its children, if any).
    /// </summary>
    double TotalMass { get; }

    /// <summary>
    /// The weighted-average position of node's body or of all of its children, if any.
    /// This acts as the single point from which the node's TotalMass exerts its gravitational force.
    /// </summary>
    Vector2D CenterOfMass { get; }

    /// <summary>
    /// Inserts a celestial body into the QuadTree starting from this node.
    /// Attempting to insert a body after the node has been evaluated will throw an exception!
    /// </summary>
    /// <param name="body">The celestial body to insert.</param>
    /// <returns>True if the body was successfully inserted into this node or one of its
    /// children; false if the body is outside this node's boundary.</returns>
    bool Insert(ICelestialBody body);

    /// <summary>
    /// Does a recursive, bottom-up calculation of the total mass and the center of mass of this node.
    /// Must only be called on the root node after all bodies have been inserted into the tree.
    /// Once called, locks the node so any further attempts to modify it will throw an exception.
    /// </summary>
    void Evaluate();
}


/// <summary>
/// Represents a node in the QuadTree. Can be:
/// <list type="bullet">
///     <item>
///         <term>External</term>
///         <description>Contains celestial bodies</description>
///     </item>
///     <item>
///         <term>Internal</term>
///         <description>Contains four child nodes</description>
///     </item>
/// </list>
/// Once the node has been evaluated, any further attempts to modify it will throw an exception.
/// </summary>
internal class QuadTreeNode : IQuadTreeNode
{
    internal QuadTreeNode(AABB boundary) : this(boundary, 0) { }

    private QuadTreeNode(AABB boundary, int depth)
    {
        Boundary = boundary;
        _depth = depth;
    }

    #region Fields

    /// <summary>
    /// The maximum number of recursive subdivisions. A safeguard to prevent stack overflows
    /// and to cap the performance cost of resolving extremely dense regions.
    /// </summary>
    static readonly int MAX_DEPTH = 32;

    /// <summary>
    /// The current depth of this node in the tree.
    /// </summary>
    private readonly int _depth = 0;

    /// <inheritdoc />
    public int Count { get; private set; } = 0;

    /// <inheritdoc />
    public bool IsEvaluated { get; private set; } = false;

    /// <inheritdoc />
    public IEnumerable<ICelestialBody> Bodies
    {
        get
        {
            if (_body != null) yield return _body;
            else if (_crowdedBodies != null)
            {
                foreach (var body in _crowdedBodies) yield return body;
            }
        }
    }

    /// <summary>
    /// The memory-optimized storage for a single body. This is used for the vast
    /// majority of external nodes and avoids the overhead of a List allocation.
    /// It is null if the node is internal, crowded, or empty.
    /// </summary>
    private ICelestialBody? _body;

    /// <summary>
    /// The storage for a "crowded" node. Is only allocated and used if a node
    /// is at MAX_DEPTH, ensuring the happy path has zero list overhead.
    /// </summary>
    private List<ICelestialBody>? _crowdedBodies;

    // Child nodes are nullable, as they only exist after subdivision.
    private QuadTreeNode? _nwChild;
    private QuadTreeNode? _neChild;
    private QuadTreeNode? _swChild;
    private QuadTreeNode? _seChild;

    /// <inheritdoc />
    IEnumerable<IQuadTreeNode> IQuadTreeNode.Children
    {
        get => PrivateChildren;
    }

    /// <summary>
    /// Private iterator to safely access the existing child nodes.
    /// </summary>
    private IEnumerable<QuadTreeNode> PrivateChildren
    {
        get
        {
            if (_nwChild != null) yield return _nwChild;
            if (_neChild != null) yield return _neChild;
            if (_swChild != null) yield return _swChild;
            if (_seChild != null) yield return _seChild;
        }
    }

    /// <inheritdoc />
    public AABB Boundary { get; init; }

    /// <inheritdoc />
    public bool IsExternal { get; private set; } = true;

    /// <summary>
    /// A flag to determine if a node is crowded or not.
    /// </summary>
    private bool IsCrowded => _depth >= MAX_DEPTH;

    /// <inheritdoc />
    public double TotalMass { get; private set; } = 0;

    /// <inheritdoc />
    public Vector2D CenterOfMass { get; private set; } = Vector2D.Zero;

    #endregion


    #region Insert

    /// <inheritdoc />
    public bool Insert(ICelestialBody body)
    {
        if (IsEvaluated) throw new InvalidOperationException("The QuadTreeNode has been evaluated and cannot be modified.");

        if (!Boundary.Contains(body)) return false;

        InsertWorker(body);
        return true;
    }

    /// <summary>
    /// Private worker that skips the redundant public-facing safety check. 
    /// </summary>
    private void InsertWorker(ICelestialBody newBody)
    {
        Debug.Assert(Boundary.Contains(newBody), "InsertWorker was called on a node that does not contain the body. Check DistributeToChild logic.");

        Count++;

        if (IsCrowded)
        {
            _crowdedBodies ??= [];
            _crowdedBodies.Add(newBody);
            return;
        }

        if (IsExternal)
        {
            if (_body == null)
            {
                _body = newBody;
                return;
            }

            IsExternal = false;
            Subdivide();
            DistributeToChild(_body);
            _body = null;
        }

        // Insert the new body into the correct child node.
        DistributeToChild(newBody);
    }

    /// <summary>
    /// Insert helper method to subdivide this node into four new child nodes.
    /// </summary>
    private void Subdivide()
    {
        var center = Boundary.Center;
        var newHalfDim = Boundary.HalfDimension / 2.0;

        var nwCenter = center + new Vector2D(-newHalfDim.X, newHalfDim.Y);
        var neCenter = center + new Vector2D(newHalfDim.X, newHalfDim.Y);
        var swCenter = center + new Vector2D(-newHalfDim.X, -newHalfDim.Y);
        var seCenter = center + new Vector2D(newHalfDim.X, -newHalfDim.Y);

        _nwChild = new QuadTreeNode(new AABB(nwCenter, newHalfDim), _depth + 1);
        _neChild = new QuadTreeNode(new AABB(neCenter, newHalfDim), _depth + 1);
        _swChild = new QuadTreeNode(new AABB(swCenter, newHalfDim), _depth + 1);
        _seChild = new QuadTreeNode(new AABB(seCenter, newHalfDim), _depth + 1);
    }

    /// <summary>
    /// Insert helper method to pass a body down to the correct child node.
    /// </summary>
    private void DistributeToChild(ICelestialBody body)
    {
        Debug.Assert(!IsExternal, "DistributeToChild should only be called on internal nodes.");

        var center = Boundary.Center;
        var pos = body.Position;

        if (pos.X < center.X)
        {
            if (pos.Y < center.Y) _swChild!.InsertWorker(body);
            else _nwChild!.InsertWorker(body);
        }
        else
        {
            if (pos.Y < center.Y) _seChild!.InsertWorker(body);
            else _neChild!.InsertWorker(body);
        }
    }

    #endregion

    #region Mass Distribution Calculation

    /// <inheritdoc />
    public void Evaluate()
    {
        if (IsEvaluated) throw new InvalidOperationException("The QuadTreeNode has been evaluated and cannot be modified.");
        EvaluateWorker();
    }

    /// <summary>
    /// Private worker that skips the redundant public-facing safety check. 
    /// </summary>
    private void EvaluateWorker()
    {
        IsEvaluated = true;

        if (IsCrowded) CalcMassDistCrowded();
        else if (IsExternal) CalcMassDistSimple();
        else CalcMassDistInternal();
    }

    /// <summary>
    /// Calculation helper for a simple external node, which contains at most one body.
    /// </summary>
    private void CalcMassDistSimple()
    {
        if (_body == null) return;  // If no body is contained within, the TotalMass remains 0.
        TotalMass = _body.Mass;

        if (TotalMass == 0) return; // If TotalMass is 0, CenterOfMass remains at its initial (0,0) position.
        CenterOfMass = _body.Position;
    }

    /// <summary>
    /// Calculation helper for a "crowded" external node, which is at the maximum
    /// depth and may contain multiple bodies.
    /// </summary>
    private void CalcMassDistCrowded()
    {
        if (_crowdedBodies == null) return;

        Vector2D weightedPositionSum = Vector2D.Zero;
        foreach (var body in _crowdedBodies)
        {
            TotalMass += body.Mass;
            weightedPositionSum += body.Position * body.Mass;
        }

        if (TotalMass == 0) return;
        CenterOfMass = weightedPositionSum / TotalMass;
    }

    /// <summary>
    /// Calculation helper method to calculate the total mass and center of mass
    /// of an internal node.
    /// </summary>
    private void CalcMassDistInternal()
    {
        Vector2D weightedPositionSum = Vector2D.Zero;

        // Recursively evaluate children and aggregate their mass properties.
        // Each child's CenterOfMass is treated as a single point-mass.
        foreach (var child in PrivateChildren)
        {
            child.EvaluateWorker();
            TotalMass += child.TotalMass;
            weightedPositionSum += child.CenterOfMass * child.TotalMass;
        }

        if (TotalMass == 0) return; // If TotalMass is 0, CenterOfMass remains at its initial (0,0) position.

        CenterOfMass = weightedPositionSum / TotalMass;
    }

    #endregion
}