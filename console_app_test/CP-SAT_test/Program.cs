
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


//最大化 2x + 2y + 3z 并受到以下限制条件的约束：
/**
x + 7⁄2 y + 3⁄2 z	≤	25
3x - 5y + 7z	≤	45
5x + 2y - 6z	≤	37
x, y, z	≥	0
**/
//CP-SAT 求解器 限制条件和目标都必须是整数 系数，所以要将x + 7⁄2 y + 3⁄2 z	≤	25转换为2x + 7y + 3z ≤ 50
CpModel model22 = new CpModel();

// Creates the variables.
int varUpperBound = new int[] { 50, 45, 37 }.Max();

IntVar a = model22.NewIntVar(0, varUpperBound, "a");
IntVar b = model22.NewIntVar(0, varUpperBound, "b");
IntVar c = model22.NewIntVar(0, varUpperBound, "c");

model22.Add(2 * a + 7 * b + 3 * c <= 50);
model22.Add(3 * a - 5 * b + 7 * c <= 45);
model22.Add(5 * a + 2 * b - 6 * c <= 37);

//这行代码是 CP-SAT 求解器的目标函数设置，它告诉求解器：在所有满足约束条件的解中，找出使 2a + 2b + 3c 值最大的那个解。
model22.Maximize(2 * a + 2 * b + 3 * c);
CpSolver solver22 = new CpSolver();
CpSolverStatus status22 = solver22.Solve(model22);

if (status22 == CpSolverStatus.Optimal || status22 == CpSolverStatus.Feasible)
{
    Console.WriteLine($"Maximum of objective function: {solver22.ObjectiveValue}");
    Console.WriteLine("a = " + solver22.Value(a));
    Console.WriteLine("b = " + solver22.Value(b));
    Console.WriteLine("c = " + solver22.Value(c));
}
else
{
    Console.WriteLine("No solution found.");
}

Console.WriteLine("Statistics");
Console.WriteLine($"  conflicts: {solver22.NumConflicts()}");
Console.WriteLine($"  branches : {solver22.NumBranches()}");
Console.WriteLine($"  wall time: {solver22.WallTime()}s");


