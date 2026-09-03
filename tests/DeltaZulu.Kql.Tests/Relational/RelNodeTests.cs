using DeltaZulu.Kql.Relational;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Kql.Tests.Relational;

/// <summary>
/// Invariant tests for the backend-neutral relational IR. These are structural —
/// they verify that records preserve constructor values and that ordering-sensitive
/// collections are not silently reordered — not translator behavior tests.
/// </summary>
[TestClass]
public sealed class RelNodeTests
{
    [TestMethod]
    public void ScanNode_PreservesViewName()
    {
        var node = new ScanNode("golden.ProcessEvent");
        Assert.AreEqual("golden.ProcessEvent", node.ViewName);
    }

    [TestMethod]
    public void FilterNode_PreservesInputAndPredicate()
    {
        var input = new ScanNode("t");
        var predicate = new LiteralScalar(true, LiteralKind.Bool);
        var node = new FilterNode(input, predicate);

        Assert.AreSame(input, node.Input);
        Assert.AreSame(predicate, node.Predicate);
    }

    [TestMethod]
    public void ProjectNode_PreservesProjectionOrder()
    {
        var projections = new[]
        {
            new ProjectionExpr("A", new ColumnRef("A")),
            new ProjectionExpr("B", new ColumnRef("B")),
            new ProjectionExpr("C", new ColumnRef("C")),
        };
        var node = new ProjectNode(new ScanNode("t"), projections);

        CollectionAssert.AreEqual(
            projections.Select(p => p.Alias).ToArray(),
            node.Projections.Select(p => p.Alias).ToArray());
    }

    [TestMethod]
    public void DistinctNode_PreservesProjectionOrder()
    {
        var projections = new[]
        {
            new ProjectionExpr("Z", new ColumnRef("Z")),
            new ProjectionExpr("A", new ColumnRef("A")),
        };
        var node = new DistinctNode(new ScanNode("t"), projections);

        CollectionAssert.AreEqual(
            projections.Select(p => p.Alias).ToArray(),
            node.Projections.Select(p => p.Alias).ToArray());
    }

    [TestMethod]
    public void AggregateNode_PreservesGroupByOrder()
    {
        var groupBy = new ScalarExpr[] { new ColumnRef("A"), new ColumnRef("B"), new ColumnRef("C") };
        var node = new AggregateNode(
            new ScanNode("t"),
            [new ProjectionExpr("Count", new FunctionCall("count", []))],
            groupBy);

        CollectionAssert.AreEqual(groupBy, node.GroupBy.ToArray());
    }

    [TestMethod]
    public void SortNode_PreservesSortOrder()
    {
        var sorts = new[]
        {
            new SortExpr(new ColumnRef("A"), SortDirection.Asc),
            new SortExpr(new ColumnRef("B"), SortDirection.Desc),
        };
        var node = new SortNode(new ScanNode("t"), sorts);

        CollectionAssert.AreEqual(sorts, node.Sorts.ToArray());
    }

    [TestMethod]
    public void ListScalar_PreservesItemOrder()
    {
        var items = new ScalarExpr[]
        {
            new LiteralScalar(1L, LiteralKind.Long),
            new LiteralScalar(2L, LiteralKind.Long),
            new LiteralScalar(3L, LiteralKind.Long),
        };
        var list = new ListScalar(items);

        CollectionAssert.AreEqual(items, list.Items.ToArray());
    }

    [TestMethod]
    public void JoinNode_QualifiersRemainLeftAndRight()
    {
        var predicate = new BinaryScalar(
            new ColumnRef("Id", JoinSide.Left),
            ScalarBinaryOp.Eq,
            new ColumnRef("Id", JoinSide.Right));

        var node = new JoinNode(new ScanNode("l"), new ScanNode("r"), JoinKind.Inner, predicate);

        var onPredicate = (BinaryScalar)node.OnPredicate;
        Assert.AreEqual("$left", ((ColumnRef)onPredicate.Left).Qualifier);
        Assert.AreEqual("$right", ((ColumnRef)onPredicate.Right).Qualifier);
        Assert.AreEqual(JoinFlavor.GenericJoin, node.Flavor);
    }

    [TestMethod]
    public void JoinNode_LookupFlavorIsPreserved()
    {
        var node = new JoinNode(
            new ScanNode("l"),
            new ScanNode("r"),
            JoinKind.LeftOuter,
            new LiteralScalar(true, LiteralKind.Bool),
            JoinFlavor.Lookup);

        Assert.AreEqual(JoinFlavor.Lookup, node.Flavor);
    }

    [TestMethod]
    public void WindowBound_SurvivesConstruction()
    {
        var precedingBound = new WindowBound(WindowBoundKind.Preceding, new LiteralScalar(5L, LiteralKind.Long));
        var currentRow = new WindowBound(WindowBoundKind.CurrentRow);

        Assert.AreEqual(WindowBoundKind.Preceding, precedingBound.Kind);
        Assert.IsNotNull(precedingBound.Offset);
        Assert.AreEqual(WindowBoundKind.CurrentRow, currentRow.Kind);
        Assert.IsNull(currentRow.Offset);
    }

    [TestMethod]
    public void WindowFrame_PreservesStartAndEnd()
    {
        var start = new WindowBound(WindowBoundKind.UnboundedPreceding);
        var end = new WindowBound(WindowBoundKind.CurrentRow);
        var frame = new WindowFrame(WindowFrameType.Rows, start, end);

        Assert.AreEqual(WindowFrameType.Rows, frame.Type);
        Assert.AreSame(start, frame.Start);
        Assert.AreSame(end, frame.End);
    }

    [TestMethod]
    public void WindowSpec_PreservesPartitionAndOrderOrder()
    {
        var partitionBy = new ScalarExpr[] { new ColumnRef("A"), new ColumnRef("B") };
        var orderBy = new[]
        {
            new SortExpr(new ColumnRef("Timestamp"), SortDirection.Desc),
        };
        var spec = new WindowSpec(partitionBy, orderBy);

        CollectionAssert.AreEqual(partitionBy, spec.PartitionBy.ToArray());
        CollectionAssert.AreEqual(orderBy, spec.OrderBy.ToArray());
        Assert.IsNull(spec.Frame);
    }

    [TestMethod]
    public void LetBindingNode_PreservesScalarOrTabularValue()
    {
        var scalarLet = new LetBindingNode("x", TabularValue: null, ScalarValue: new LiteralScalar(1L, LiteralKind.Long), Body: new ScanNode("t"));
        var tabularLet = new LetBindingNode("y", TabularValue: new ScanNode("s"), ScalarValue: null, Body: new ScanNode("t"));

        Assert.IsNull(scalarLet.TabularValue);
        Assert.IsNotNull(scalarLet.ScalarValue);
        Assert.IsNotNull(tabularLet.TabularValue);
        Assert.IsNull(tabularLet.ScalarValue);
    }

    [TestMethod]
    public void CaseScalar_PreservesBranchOrderAndElse()
    {
        var branches = new List<(ScalarExpr When, ScalarExpr Then)>
        {
            (new LiteralScalar(true, LiteralKind.Bool), new LiteralScalar("a", LiteralKind.String)),
            (new LiteralScalar(false, LiteralKind.Bool), new LiteralScalar("b", LiteralKind.String)),
        };
        var elseExpr = new LiteralScalar("c", LiteralKind.String);
        var caseExpr = new CaseScalar(branches, elseExpr);

        CollectionAssert.AreEqual(branches, caseExpr.Branches.ToArray());
        Assert.AreSame(elseExpr, caseExpr.Else);
    }

    [TestMethod]
    public void RelNode_RecordEquality_IsStructuralByReferenceForLists()
    {
        // Records with IReadOnlyList<T> members compare that member by reference,
        // not by content — two ScanNode-wrapping ProjectNodes built with separately
        // allocated (but content-equal) lists are NOT equal. This is a fact about
        // the IR's equality semantics that any structural comparer must account for.
        var a = new ProjectNode(new ScanNode("t"), [new ProjectionExpr("A", new ColumnRef("A"))]);
        var b = new ProjectNode(new ScanNode("t"), [new ProjectionExpr("A", new ColumnRef("A"))]);

        Assert.AreNotEqual(a, b);
        Assert.AreEqual(a, a);
    }
}
