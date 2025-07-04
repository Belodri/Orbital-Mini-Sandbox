using Physics.Bodies;
using Physics.Models;

namespace Physics.Core;


/// <summary>
/// Axis-Aligned Bounding Box.<br/>
/// Represents a rectangular area in 2D space,
/// defined by a center point and its half-dimensions (half-width and half-height).
/// </summary>
internal readonly struct AABB(Vector2D center, Vector2D halfDimension)
{
    /// <summary>
    /// The geometric center of the bounding rectangle.
    /// </summary>
    internal Vector2D Center { get; } = center;

    /// <summary>
    /// A vector representing half the width and half the height of the box.
    /// This is used for efficient calculation of the box's corners.
    /// </summary>
    internal Vector2D HalfDimension { get; } = new(Math.Abs(halfDimension.X), Math.Abs(halfDimension.Y));   // Ensure positive HalfDimension

    /// <summary>
    /// The minimum corner of the box (bottom-left).
    /// </summary>
    internal Vector2D Min => Center - HalfDimension;

    /// <summary>
    /// The maximum corner of the box (top-right).
    /// </summary>
    internal Vector2D Max => Center + HalfDimension;

    /// <summary>
    /// Checks if a celestial body's position is contained within this bounding box.
    /// The check is inclusive of the minimum boundary and exclusive of the maximum boundary
    /// [min, max) to ensure bodies on an edge belong to only one quadrant.
    /// </summary>
    /// <param name="body">The celestial body to check.</param>
    /// <returns>True if the body is inside the boundary, otherwise false.</returns>
    internal bool Contains(CelestialBody body)
    {
        var pos = body.Position;
        var min = Min;
        var max = Max;
        return pos.X >= min.X && pos.X < max.X &&
                pos.Y >= min.Y && pos.Y < max.Y;
    }
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
/// </summary>
internal class QuadTreeNode(AABB boundary, int depth = 0)
{
    #region Fields

    /// <summary>
    /// The maximum number of recursive subdivisions. A safeguard to prevent stack overflows
    /// and to cap the performance cost of resolving extremely dense regions.
    /// </summary>
    static readonly int MAX_DEPTH = 32;

    /// <summary>
    /// The current depth of this node in the tree.
    /// </summary>
    readonly int _depth = depth;

    /// <summary>
    /// Tracks how many bodies are in this node and all of its children.
    /// </summary>
    internal int Count = 0;

    /// <summary>
    /// The memory-optimized storage for a single body. This is used for the vast
    /// majority of external nodes and avoids the overhead of a List allocation.
    /// It is null if the node is internal, crowded, or empty.
    /// </summary>
    CelestialBody? _body;

    /// <summary>
    /// The storage for a "crowded" node. Is only allocated and used if a node
    /// is at MAX_DEPTH, ensuring the happy path has zero list overhead.
    /// </summary>
    List<CelestialBody>? _crowdedBodies;

    // Child nodes are nullable, as they only exist after subdivision.
    QuadTreeNode? _nwChild;
    QuadTreeNode? _neChild;
    QuadTreeNode? _swChild;
    QuadTreeNode? _seChild;

    /// <summary>
    /// The rectangular boundary that this node represents in the simulation space.
    /// </summary>
    internal AABB Boundary { get; } = boundary;

    /// <summary>
    /// A flag indicating whether this node is a external or an internal node.
    /// </summary>
    internal bool IsExternal { get; private set; } = true;

    /// <summary>
    /// A flag to determine if a node is crowded or not.
    /// </summary>
    bool IsCrowded { get => _depth >= MAX_DEPTH; }

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

    #region Insert

    /// <summary>
    /// Inserts a celestial body into the QuadTree starting from this node.
    /// </summary>
    /// <param name="newBody">The celestial body to insert.</param>
    /// <returns>True if the body was successfully inserted into this node or one of its
    /// children; false if the body is outside this node's boundary.</returns>
    internal bool Insert(CelestialBody newBody)
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
    void Subdivide()
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
    bool DistributeToChild(CelestialBody body)
    {
        // Attempt to insert into each child. The 'Insert' method's boundary check
        // will ensure only one of them succeeds.
        if (_nwChild!.Insert(body)) return true;
        if (_neChild!.Insert(body)) return true;
        if (_swChild!.Insert(body)) return true;
        if (_seChild!.Insert(body)) return true;

        // This part should theoretically not be reached if the AABB.Contains logic is correct
        // and covers the entire parent boundary without gaps or overlaps.
        throw new InvalidOperationException("Failed to distribute body to any child node. This indicates a boundary logic error.");
    }

    #endregion

    #region Mass Distribution Calculation

    /// <summary>
    /// Does a recursive, bottom-up calculation of the total mass and the center of mass of this node.
    /// Must only be called on the root node after all bodies have been inserted into the tree.
    /// </summary>
    internal void CalculateMassDistribution()
    {
        if (IsCrowded) CalcMassDistCrowded();
        else if (IsExternal) CalcMassDistSimple();
        else CalcMassDistInternal();
    }

    /// <summary>
    /// Calculation helper for a simple external node, which contains at most one body.
    /// </summary>
    void CalcMassDistSimple()
    {
        if (_body == null) return;  // If no body is contained within, the TotalMass remains 0.
        TotalMass = _body.Mass;

        if (TotalMass <= 0) return; // If TotalMass is 0, CenterOfMass remains at its initial (0,0) position.
        CenterOfMass = _body.Position;
    }

    /// <summary>
    /// Calculation helper for a "crowded" external node, which is at the maximum
    /// depth and may contain multiple bodies.
    /// </summary>
    void CalcMassDistCrowded()
    {
        if (_crowdedBodies == null) return;

        Vector2D weightedPositionSum = Vector2D.Zero;
        foreach (var body in _crowdedBodies)
        {
            TotalMass += body.Mass;
            weightedPositionSum += body.Position * body.Mass;
        }

        if (TotalMass <= 0) return;
        CenterOfMass = weightedPositionSum / TotalMass;
    }

    /// <summary>
    /// Calculation helper method to calculate the total mass and center of mass
    /// of an internal node.
    /// </summary>
    void CalcMassDistInternal()
    {
        // Recursively call this method on all children first. This ensures
        // their mass properties are calculated before we use them.
        _nwChild!.CalculateMassDistribution();
        _neChild!.CalculateMassDistribution();
        _swChild!.CalculateMassDistribution();
        _seChild!.CalculateMassDistribution();

        TotalMass = _nwChild.TotalMass
            + _neChild.TotalMass
            + _swChild.TotalMass
            + _seChild.TotalMass;

        if (TotalMass <= 0) return; // If TotalMass is 0, CenterOfMass remains at its initial (0,0) position.

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