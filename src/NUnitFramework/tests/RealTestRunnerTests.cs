using System.CodeDom.Compiler;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using Microsoft.CSharp;

namespace NUnit.Core.Tests
{
	using System;
	using NUnit.Framework;

	[TestFixture]
	public class RealTestRunnerTests
	{
		const string TEST_OUT_TEXT = "__TEST_OUT_TEXT__";
		const string TEST_ERROR_TEXT = "__TEST_ERROR_TEXT__";
		const char TEST_OUT_CHAR = 'X';
		string outputName;
		TestRunner testRunner;

		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
			CSharpCodeProvider provider = new CSharpCodeProvider();
			ICodeCompiler compiler = provider.CreateCompiler();
			string[] assemblyNames = new string[]
			{
				typeof(TestAttribute).Assembly.Location
			};
			this.outputName = "RealTestRunnerTests.dll";
			CompilerParameters options = new CompilerParameters(assemblyNames, this.outputName);
			string source = 
				@"
using System;
using System.Threading;
using NUnit.Framework;

[TestFixture]
public class MyFixture
{
	[Test] public void TestOut()
	{
		Console.WriteLine(""$(TestOutText)"");
	}

	[Test] public void TestError()
	{
		Console.WriteLine(""$(TestErrorText)"");
	}

	[Test] public void TestOutNoFlush()
	{
		Console.Write(""$(TestOutText)"");
	}

	[Test] public void TestOutChar()
	{
		Console.Write('$(TestOutChar)');
	}

	[Test] public void TestSleep()
	{
		Thread.Sleep(10 * 1000);
	}
}

[TestFixture]
public class SleepFixture
{
	[Test] public void Sleep_1() { Thread.Sleep(1000); }
	[Test] public void Sleep_2() { Thread.Sleep(1000); }
	[Test] public void Sleep_3() { Thread.Sleep(1000); }
	[Test] public void Sleep_4() { Thread.Sleep(1000); }
	[Test] public void Sleep_5() { Thread.Sleep(1000); }
}
";
			source = source.Replace("$(TestOutText)", TEST_OUT_TEXT);
			source = source.Replace("$(TestErrorText)", TEST_ERROR_TEXT);
			source = source.Replace("$(TestOutChar)", TEST_OUT_CHAR.ToString());
			CompilerResults results = compiler.CompileAssemblyFromSource(options, source);
			foreach(object error in results.Errors)
			{
				Debug.WriteLine(error);
			}
			Assert.AreEqual(0, results.NativeCompilerReturnValue);
		}

		[SetUp]
		public void SetUp()
		{
			this.testRunner = new RealTestRunner();
			this.testRunner.Load(this.outputName);
		}

		[Test]
		public void TestOut()
		{
			MockEventListener listener = new MockEventListener();
			string[] testNames = new string[] {"MyFixture.TestOut"};
			this.testRunner.Run(listener, testNames);

			int outputIndex = findType(listener.EventObjects, typeof(TestOutput));
			int startIndex = findType(listener.EventObjects, typeof(NUnit.Core.TestCase));
			int finishIndex = findType(listener.EventObjects, typeof(TestCaseResult));

			Assert.AreEqual(startIndex + 1, outputIndex, "Check that output comes after test started");
			Assert.AreEqual(finishIndex - 1, outputIndex, "Check that output comes before test finished");

			TestOutput output = (TestOutput)listener.EventObjects[outputIndex];
			Assert.AreEqual(TEST_OUT_TEXT + Environment.NewLine, output.Text);
		}

		[Test]
		public void TestError()
		{
			MockEventListener listener = new MockEventListener();
			string[] testNames = new string[] {"MyFixture.TestError"};
			this.testRunner.Run(listener, testNames);

			int errorIndex = findType(listener.EventObjects, typeof(TestOutput));
			int startIndex = findType(listener.EventObjects, typeof(NUnit.Core.TestCase));
			int finishIndex = findType(listener.EventObjects, typeof(TestCaseResult));

			Assert.AreEqual(startIndex + 1, errorIndex, "Check that error comes after test started");
			Assert.AreEqual(finishIndex - 1, errorIndex, "Check that error comes before test finished");

			TestOutput output = (TestOutput)listener.EventObjects[errorIndex];
			Assert.AreEqual(TEST_ERROR_TEXT + Environment.NewLine, output.Text);
		}

		[Test]
		public void TestOutNoFlush()
		{
			MockEventListener listener = new MockEventListener();
			string[] testNames = new string[] {"MyFixture.TestOutNoFlush"};
			this.testRunner.Run(listener, testNames);

			int outputIndex = findType(listener.EventObjects, typeof(TestOutput));
			int startIndex = findType(listener.EventObjects, typeof(NUnit.Core.TestCase));
			int finishIndex = findType(listener.EventObjects, typeof(TestCaseResult));

			Assert.AreEqual(startIndex + 1, outputIndex, "Check that output comes after test started");
			Assert.AreEqual(finishIndex - 1, outputIndex, "Check that output comes before test finished");

			TestOutput output = (TestOutput)listener.EventObjects[outputIndex];
			Assert.AreEqual(TEST_OUT_TEXT, output.Text);
		}

		[Test]
		public void TestOutChar()
		{
			MockEventListener listener = new MockEventListener();
			string[] testNames = new string[] {"MyFixture.TestOutChar"};
			this.testRunner.Run(listener, testNames);

			int outputIndex = findType(listener.EventObjects, typeof(TestOutput));
			int startIndex = findType(listener.EventObjects, typeof(NUnit.Core.TestCase));
			int finishIndex = findType(listener.EventObjects, typeof(TestCaseResult));

			Assert.AreEqual(startIndex + 1, outputIndex, "Check that output comes after test started");
			Assert.AreEqual(finishIndex - 1, outputIndex, "Check that output comes before test finished");

			TestOutput output = (TestOutput)listener.EventObjects[outputIndex];
			Assert.AreEqual(TEST_OUT_CHAR.ToString(), output.Text);
		}

		[Test, Explicit]
		public void TestCancelSleepOne()
		{
			MockEventListener listener = new MockEventListener();
			string[] testNames = new string[] {"MyFixture.TestSleep"};
			RunWorkItem workItem = new RunWorkItem(this.testRunner, listener, testNames);
			Thread thread = new Thread(new ThreadStart(workItem.Run));
			thread.Start();
			Thread.Sleep(1000);
			this.testRunner.CancelRun();
			Thread.Sleep(1000);		// How long before thread aborts?
			Assert.IsFalse(thread.IsAlive, "Check that test run has been canceled");
		}

		[Test, Explicit]
		public void TestCancelSleepMany()
		{
			MockEventListener listener = new MockEventListener();
			string[] testNames = new string[] {"SleepFixture"};
			RunWorkItem workItem = new RunWorkItem(this.testRunner, listener, testNames);
			Thread thread = new Thread(new ThreadStart(workItem.Run));
			thread.Start();
			Thread.Sleep(1000);
			this.testRunner.CancelRun();
			Thread.Sleep(1000);
			Assert.IsFalse(thread.IsAlive, "Check that test run has been canceled");
		}

		class RunWorkItem
		{
			TestRunner testRunner;
			MockEventListener listener;
			string[] testNames;

			public RunWorkItem(TestRunner testRunner, MockEventListener listener, string[] testNames)
			{
				this.testRunner = testRunner;
				this.listener = listener;
				this.testNames = testNames;
			}

			public void Run()
			{
				this.testRunner.Run(this.listener, this.testNames);
			}
		}

		int findType(IList objects, Type type)
		{
			for(int count = 0 ; count < objects.Count ; count++)
			{
				Type objectType = objects[count].GetType();
				if(type.IsAssignableFrom(objectType))
				{
					return count;
				}
			}
			throw new ApplicationException("Couldn't find object of type " + type);
		}

		class MockEventListener : EventListener
		{
			ArrayList eventObjects = new ArrayList();

			public IList EventObjects
			{
				get
				{
					return this.eventObjects;
				}
			}

			public void TestStarted(NUnit.Core.TestCase testCase)
			{
				this.eventObjects.Add(testCase);
			}

			public void RunStarted(Test[] tests)
			{
				this.eventObjects.Add(tests);
			}

			public void RunFinished(Exception exception)
			{
				this.eventObjects.Add(exception);
			}

			public void RunFinished(TestResult[] results)
			{
				this.eventObjects.Add(results);
			}

			public void SuiteFinished(TestSuiteResult result)
			{
				this.eventObjects.Add(result);
			}

			public void TestFinished(TestCaseResult result)
			{
				this.eventObjects.Add(result);
			}

			public void UnhandledException(Exception exception)
			{
				this.eventObjects.Add(exception);
			}

			public void TestOutput(TestOutput testOutput)
			{
				this.eventObjects.Add(testOutput);
			}

			public void SuiteStarted(TestSuite suite)
			{
				this.eventObjects.Add(suite);
			}
		}
	}
}