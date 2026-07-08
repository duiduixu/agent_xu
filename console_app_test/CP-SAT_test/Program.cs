
using System;
using Google.OrTools.Sat;


CpModel model = new CpModel();
int numVars = 3;
IntVar x = model.NewIntVar(0, numVars, "x");
IntVar y = model.NewIntVar(0, numVars, "y");
IntVar z = model.NewIntVar(0, numVars, "z");
model.Add(x != y);
model.Add(y != z);
model.Add(x == z);
CpSolver solver = new CpSolver();
CpSolverStatus status = solver.Solve(model);

if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
{
    Console.WriteLine("x = " + solver.Value(x));
    Console.WriteLine("y = " + solver.Value(y));
    Console.WriteLine("z = " + solver.Value(z));
}
else
{
    Console.WriteLine("No solution found.");
}





