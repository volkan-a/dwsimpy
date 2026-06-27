using Mapack;

var tests = new (string Name, Action Body)[]
{
    ("simpson integrates polynomial", SimpsonIntegratesPolynomial),
    ("mapack solves linear system", MapackSolvesLinearSystem),
    ("mapack decompositions expose expected values", MapackDecompositionsExposeExpectedValues),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    return 1;
}

Console.WriteLine($"PASS {tests.Length} tests");
return 0;

static void SimpsonIntegratesPolynomial()
{
    var result = SimpsonIntegrator.Integrate(x => x * x, 0.0, 3.0, 1e-12);
    AssertNear(9.0, result, 1e-9, "Integral of x^2 from 0 to 3");
}

static void MapackSolvesLinearSystem()
{
    var coefficients = new Matrix(new[]
    {
        new[] { 3.0, 2.0 },
        new[] { 1.0, 2.0 },
    });
    var rhs = new Matrix(new[]
    {
        new[] { 5.0 },
        new[] { 5.0 },
    });

    var solution = coefficients.Solve(rhs);

    AssertNear(0.0, solution[0, 0], 1e-10, "Linear solve x");
    AssertNear(2.5, solution[1, 0], 1e-10, "Linear solve y");
}

static void MapackDecompositionsExposeExpectedValues()
{
    var matrix = new Matrix(new[]
    {
        new[] { 4.0, 1.0 },
        new[] { 1.0, 3.0 },
    });

    var cholesky = new CholeskyDecomposition(matrix);
    Assert(cholesky.Symmetric, "Cholesky symmetry flag");
    Assert(cholesky.PositiveDefinite, "Cholesky positive definite flag");

    var lu = new LuDecomposition(matrix);
    Assert(lu.NonSingular, "LU non-singular flag");
    AssertNear(11.0, lu.Determinant, 1e-10, "LU determinant");

    var eigen = new EigenvalueDecomposition(matrix);
    var eigenvalues = eigen.RealEigenvalues.OrderBy(v => v).ToArray();
    AssertNear((7.0 - Math.Sqrt(5.0)) / 2.0, eigenvalues[0], 1e-10, "Small eigenvalue");
    AssertNear((7.0 + Math.Sqrt(5.0)) / 2.0, eigenvalues[1], 1e-10, "Large eigenvalue");

    var svd = new SingularValueDecomposition(matrix);
    Assert(svd.Rank == 2, "SVD rank");
    AssertNear(eigenvalues[1], svd.Norm2, 1e-10, "SVD 2-norm");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertNear(double expected, double actual, double tolerance, string message)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
