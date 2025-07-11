using System.Diagnostics;
using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;

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
internal class QuadTreeNode
{
    internal QuadTreeNode(AABB boundary) : this(boundary, 0) {}

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

    /// <summary>
    /// Tracks how many bodies are in this node and all of its children.
    /// </summary>
    internal int Count { get; private set; } = 0;

    /// <summary>
    /// Tracks if the node has been evaluated and is locked from further modification.
    /// </summary>
    internal bool IsEvaluated { get; private set; } = false;

    internal int? BodyId => _body?.Id;

    /// <summary>
    /// The memory-optimized storage for a single body. This is used for the vast
    /// majority of external nodes and avoids the overhead of a List allocation.
    /// It is null if the node is internal, crowded, or empty.
    /// </summary>
    private CelestialBody? _body;

    /// <summary>
    /// The storage for a "crowded" node. Is only allocated and used if a node
    /// is at MAX_DEPTH, ensuring the happy path has zero list overhead.
    /// </summary>
    private List<CelestialBody>? _crowdedBodies;

    // Child nodes are nullable, as they only exist after subdivision.
    private QuadTreeNode? _nwChild;
    private QuadTreeNode? _neChild;
    private QuadTreeNode? _swChild;
    private QuadTreeNode? _seChild;

    /// <summary>
    /// The rectangular boundary that this node represents in the simulation space.
    /// </summary>
    internal AABB Boundary { get; init; }

    /// <summary>
    /// A flag indicating whether this node is a external or an internal node.
    /// </summary>
    internal bool IsExternal { get; private set; } = true;

    /// <summary>
    /// A flag to determine if a node is crowded or not.
    /// </summary>
    internal bool IsCrowded => _depth >= MAX_DEPTH;

    /// <summary>
    /// The combined total mass of the node (either its body or of all of its children, if any).
    /// </summary>
    internal double TotalMass { get; private set; } = 0;

    /// <summary>
    /// The weighted-average position of node's body or of all of its children, if any.
    /// This acts as the single point from which the node's TotalMass exerts its gravitational force.
    /// </summary>
    internal Vector2D CenterOfMass { get; private set; } = Vector2D.Zero;

    #endregion

    // TODO Consider adding a readonly getter for the contained body/bodies

    #region Insert

    /// <summary>
    /// Inserts a celestial body into the QuadTree starting from this node.
    /// Attempting to insert a body after the node has been evaluated will throw an exception!
    /// </summary>
    /// <param name="newBody">The celestial body to insert.</param>
    /// <returns>True if the body was successfully inserted into this node or one of its
    /// children; false if the body is outside this node's boundary.</returns>
    internal bool Insert(CelestialBody newBody)
    {
        if (IsEvaluated) throw new InvalidOperationException("The QuadTreeNode has been evaluated and cannot be modified.");
        return InsertWorker(newBody);
    }

    /// <summary>
    /// Private worker that skips the redundant public-facing safety check. 
    /// </summary>
    private bool InsertWorker(CelestialBody newBody)
    {
        // Primary escape condition for recursion.
        if (!Boundary.Contains(newBody)) return false;

        Count++;

        if (IsCrowded)
        {
            _crowdedBodies ??= [];
            _crowdedBodies.Add(newBody);
            return true;
        }

        if (IsExternal)
        {
            if (_body == null)
            {
                _body = newBody;
                return true;
            }

            IsExternal = false;
            Subdivide();
            DistributeToChild(_body);
            _body = null;
        }

        // Insert the new body into the correct child node.
        return DistributeToChild(newBody);
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
    private bool DistributeToChild(CelestialBody body)
    {
        // Attempt to insert into each child. The 'InsertWorker' method's boundary check
        // will ensure only one of them succeeds.
        if (_nwChild!.InsertWorker(body)) return true;
        if (_neChild!.InsertWorker(body)) return true;
        if (_swChild!.InsertWorker(body)) return true;
        if (_seChild!.InsertWorker(body)) return true;

        // This part should theoretically not be reached if the AABB.Contains logic is correct
        // and covers the entire parent boundary without gaps or overlaps.
        throw new UnreachableException("Failed to distribute body to any child node. This indicates a boundary logic error.");
    }

    #endregion

    #region Mass Distribution Calculation

    /// <summary>
    /// Does a recursive, bottom-up calculation of the total mass and the center of mass of this node.
    /// Must only be called on the root node after all bodies have been inserted into the tree.
    /// Once called, locks the node so any further attempts to modify it will throw an exception.
    /// </summary>
    internal void Evaluate()
    {
        if (IsEvaluated) throw new InvalidOperationException("The QuadTreeNode has been evaluated and cannot be modified.");
        EvaluateWorker();
    }

    /// <summary>
    /// Private worker that skips the redundant public-facing safety check. 
    /// </summary>
    void EvaluateWorker()
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
        // Recursively call this method on all children first. This ensures
        // their mass properties are calculated before we use them.
        _nwChild!.EvaluateWorker();
        _neChild!.EvaluateWorker();
        _swChild!.EvaluateWorker();
        _seChild!.EvaluateWorker();

        TotalMass = _nwChild.TotalMass
            + _neChild.TotalMass
            + _swChild.TotalMass
            + _seChild.TotalMass;

        if (TotalMass == 0) return; // If TotalMass is 0, CenterOfMass remains at its initial (0,0) position.

        // Each child's CenterOfMass is treated as a single point-mass.
        // We calculate the weighted sum of their centers of mass.
        Vector2D weightedPositionSum = (_nwChild.CenterOfMass * _nwChild.TotalMass)
            + (_neChild.CenterOfMass * _neChild.TotalMass)
            + (_swChild.CenterOfMass * _swChild.TotalMass)
            + (_seChild.CenterOfMass * _seChild.TotalMass);

        CenterOfMass = weightedPositionSum / TotalMass;
    }

    #endregion
}