﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RDotNet;
using CommonSupportLib;
using System.Collections;
using System.Threading;

namespace deldir
{
   class Program
   {
      static void Main(string[] args)
      {
         SupportHelper.SetupPath();
         using (REngine engine = REngine.CreateInstance("RDotNet"))
         {
            engine.Initialize();
            //DoTest(engine);
            ReproWorkitem45(engine);
         }
      }

      private static void DoTest(REngine engine)
      {
         var setupStr = @"library(deldir)
set.seed(421)
x <- runif(20)
y <- runif(20)
z <- deldir(x,y)
w <- tile.list(z)

z <- deldir(x,y,rw=c(0,1,0,1))
w <- tile.list(z)

z <- deldir(x,y,rw=c(0,1,0,1),dpl=list(ndx=2,ndy=2))
w <- tile.list(z)
";

         engine.Evaluate(setupStr);
         var res = new List<List<Tuple<double, double>>>();
         var n = engine.Evaluate("length(w)").AsInteger()[0];
         for (int i = 1; i <= n; i++)
         {
            var x = engine.Evaluate("w[[" + i + "]]$x").AsNumeric().ToArray();
            var y = engine.Evaluate("w[[" + i + "]]$y").AsNumeric().ToArray();
            var t = x.Zip(y, (first, second) => Tuple.Create(first, second)).ToList();
            res.Add(t);
         }
      }

      private static void ReproIssue77(REngine engine)
      {
         object expr = engine.Evaluate("function(k) substitute(bar(x) = k)");
         Console.WriteLine(expr ?? "null");
      }

      private static void ReproDiscussion528955(REngine engine)
      {
         engine.Evaluate("a <- 1");
         engine.Evaluate("a <- a+1");
         NumericVector v1 = engine.GetSymbol("a").AsNumeric();
         bool eq = 2.0 == v1[0];
         engine.Evaluate("a <- a+1");
         NumericVector v2 = engine.GetSymbol("a").AsNumeric();
         eq = 3.0 == v2[0];
      }

      private static void ReproDiscussion532760(REngine engine)
      {
         // https://rdotnet.codeplex.com/discussions/532760
         //> x <- data.frame(1:1e6, row.names=format(1:1e6))
         //> object.size(x)
         //60000672 bytes
         //> object.size(rownames(x))
         //56000040 bytes

         engine.Evaluate("x <- data.frame(1:1e6, row.names=format(1:1e6))");
         var x = engine.GetSymbol("x").AsDataFrame();
         engine.ForceGarbageCollection();
         engine.ForceGarbageCollection();
         var memoryInitial = engine.Evaluate("memory.size()").AsNumeric().First();

         var netMemBefore = GC.GetTotalMemory(true);

         var blah = x.RowNames;
         //var blah = engine.Evaluate("rownames(x)").AsCharacter().ToArray();
         blah = null;

         GC.Collect();
         engine.ForceGarbageCollection();
         engine.ForceGarbageCollection();
         var memoryAfterAlloc = engine.Evaluate("memory.size()").AsNumeric().First();

         var netMemAfter = GC.GetTotalMemory(false);

      }

      /*
1.5.5
		         //var blah = x.RowNames;
		memoryInitial	50.16	double
		netMemBefore	87860	long
		blah	null	string[]
		memoryAfterAlloc	62.08	double
		netMemAfter	0	long

		
         var blah = engine.Evaluate("rownames(x)").AsCharacter().ToArray();
		memoryInitial	50.15	double
		netMemBefore	87948	long
		blah	null	string[]
		memoryAfterAlloc	65.95	double
		netMemAfter	0	long

		
		
		1.5.6, not waiting for final total memory (otherwise hangs, as with above)
		
		memoryInitial	50.16	double
		netMemBefore	88608	long
		blah	null	string[]
		memoryAfterAlloc	65.95	double
		netMemAfter	98738480	long

		
		memoryInitial	50.16	double
		netMemBefore	88572	long
		blah	null	string[]
		memoryAfterAlloc	62.08	double
		netMemAfter	99319384	long

		
		       */

      private static void ReproWorkitem41(REngine engine)
      {
         var fname = "c:/tmp/rgraph.png";
         engine.Evaluate("library(ggplot2)");
         engine.Evaluate("library(scales)");
         engine.Evaluate("library(plyr)");
         engine.Evaluate("d <- data.frame( x = rnorm(1000), y = rnorm(1000), z = rnorm(1000))");
         engine.Evaluate("p <- ggplot(d, aes(x = x, y = y, color = z)) + geom_point(size=4.5, shape=19)");
         // Use:
         engine.Evaluate("png('" + fname + "')");
         engine.Evaluate("print(p)");
         // but not:
         // engine.Evaluate("p");
         // engine.Evaluate("dev.copy(png, '" + fname + "')");
         // the statement engine.Evaluate("p") does not behave the same as p (or print(p)) directly in the R console.
         engine.Evaluate("dev.off()");
      }

      private static void ReproWorkitem43(REngine engine)
      {
         Random r = new Random(0);
         int N = 500;
         int n1 = 207;
         int n2 = 623;
         var arGroup1Intensities = new double[N][];
         var arGroup2Intensities = new double[N][];
         for (int i = 0; i < N; i++)
         {
            arGroup1Intensities[i] = new double[n1];
            arGroup2Intensities[i] = new double[n2];
            for (int j = 0; j < n1; j++)
               arGroup1Intensities[i][j] = r.NextDouble();
            for (int j = 0; j < n2; j++)
               arGroup2Intensities[i][j] = r.NextDouble();
         }
         var res = new GenericVector[N];
         NumericVector vGroup1, vGroup2;
         for (int i = 0; i < N; i++)
         {
            vGroup1 = engine.CreateNumericVector(arGroup1Intensities[i]);
            Console.WriteLine(vGroup1.Length);
            if (i % 10 == 4)
            {
               engine.ForceGarbageCollection();
               engine.ForceGarbageCollection();
            }
            vGroup2 = engine.CreateNumericVector(arGroup2Intensities[i]);
            Console.WriteLine(vGroup2.Length);
            engine.SetSymbol("group1", vGroup1);
            engine.SetSymbol("group2", vGroup2);
            GenericVector testResult = engine.Evaluate("t.test(group1, group2)").AsList();
            res[i] = testResult;
         }
      }


      #region workitem45

      // https://rdotnet.codeplex.com/workitem/45

      private static bool Started = false;
      private static ArrayList List = new ArrayList();
      private static int[] Counts = new int[256];
      private static Thread Graphics;
      private static IntegerVector group1, group2;
      private static ArrayList KeepAlive = new ArrayList();
      private static byte[] Add = new byte[1];

      private static void Repro45Thread(object rEngine)
      {
         REngine engine = (REngine)rEngine;
         for (int lines = 1; lines < 100; lines++)
         {
            //Entropy.Data Source = new Entropy.Data();
            Random Rand = new Random(0);
            for (int nums = 1; nums < 10; nums++)
            {
               //List.Add(Source.Retzme());
               Rand.NextBytes(Add);
               List.Add(Add[0]);
            }
         }
         int[] Nums = new int[List.Count];
         int Size = 0;
         foreach (byte Number in List)
         {
            Size++;
            Counts[Convert.ToInt32(Number)] = (Counts[Convert.ToInt32(Number)]) + 1;
            Nums[Size - 1] = Convert.ToInt32(Number);
         }
         Size = 0;
         if (KeepAlive.Count != 0)
         {
            engine.Close();
            engine.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            //engine = REngine.CreateInstance("R", new[] { "-q" });
         }
         else
         {
            //GC.KeepAlive(REngine.SetDllDirectory(@"C:\Program Files\R\R-3.0.1\bin\i386"));
            //engine = REngine.CreateInstance("R", new[] { "-q" });
         }
         GC.KeepAlive(engine);
         KeepAlive.Add(engine);
         group1 = engine.CreateIntegerVector(Counts);
         engine.SetSymbol("group1", group1);
         group2 = engine.CreateIntegerVector(Nums);
         GC.KeepAlive(group1);
         GC.KeepAlive(group2);
         engine.SetSymbol("group2", group2);
         engine.Evaluate("library(base)");
         engine.Evaluate("library(stats)");
         engine.Evaluate("x <- group1");
         engine.Evaluate("y <- group2");
         engine.Evaluate("windows( width=10, height=8, pointsize=8)");
         engine.Evaluate("par(yaxp = c( 0, 100, 9))");
         engine.Evaluate("par(xaxp = c( 0, 255, 24))");
         engine.Evaluate("par(cex = 1.0)");
         //Eng.Evaluate("bins=seq(0,255,by=1.0)");
         //Eng.Evaluate("hist(x:y, breaks=50, col=c(\"blue\"))");
         engine.Evaluate("plot(x, type=\"h\", col=c(\"red\"))");
         //engine.Close();
         //engine.Dispose();
         return;
      }

      public static bool ReproWorkitem45(REngine engine, int numThreads = 1)
      {
         for (int i = 0; i < numThreads; i++)
         {
            Graphics = new Thread(Repro45Thread);
            Graphics.Start(engine);
         }
         //Started = true;
         return true;
      }

      #endregion

   }
}