namespace nunit.integration.tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using System.Xml.XPath;

    using Dsl;

    using Microsoft.CodeAnalysis.VisualBasic.Syntax;

    using NUnit.Framework;
    using TechTalk.SpecFlow;

    [Binding]
    public class NUnitSteps
    {
        [Given(@"NUnit path is (.+)")]
        public void DefineNUnitConsolePath(string originNUnitPath)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            configuration.OriginNUnitPath = Path.GetFullPath(Path.Combine(ctx.AssemblyDirectory, originNUnitPath));
            var environmentManager = new EnvironmentManager();
            configuration.NUnitConsolePath = environmentManager.PrepareNUnitClonsoleAndGetPath(ctx.SandboxPath, configuration.OriginNUnitPath);
        }

        [Given(@"Framework version is (.+)")]
        public void DefineFrameworkVersion(string frameworkVersion)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            configuration.FrameworkVersion = frameworkVersion.ConvertToFrameworkVersion();            
        }

        [Given(@"I want to use (.+) configuration type")]
        public void UseConfigurationType(string configurationType)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            configuration.ConfigurationType = configurationType.ConvertToConfigurationType();            
        }

        [Given(@"I have added (.+) method as (.+) to the class (.+)\.(.+) for (.+)")]
        public void AddMethod(string methodTemplate, string testMethodName, string namespaceName, string className, string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var testAssembly = ctx.GetOrCreateAssembly(assemblyName);
            var testClass = testAssembly.GetOrCreateClass(namespaceName, className);
            testClass.GetOrCreateMethod(testMethodName, methodTemplate);            
        }

        [Given(@"I have added (\d+) (.+) methods as (.+) to the class (.+)\.(.+) for (.+)")]
        public void AddMethods(int methodsNumber, string methodTemplate, string testMethodName, string namespaceName, string className, string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var testAssembly = ctx.GetOrCreateAssembly(assemblyName);
            var testClass = testAssembly.GetOrCreateClass(namespaceName, className);
            for (var methodIndex = 0; methodIndex < methodsNumber; methodIndex++)
            {
                testClass.GetOrCreateMethod(testMethodName + "_" + methodIndex, methodTemplate);
            }
        }

        [Given(@"I have added attribute (.+) to the class (.+)\.(.+) for (.+)")]
        public void AddAttributeToClass(string attribute, string namespaceName, string className, string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var testAssembly = ctx.GetOrCreateAssembly(assemblyName);
            var testClass = testAssembly.GetOrCreateClass(namespaceName, className);
            testClass.AddAttribute(attribute);
        }

        [Given(@"I have added attribute (.+) to the assembly (.+)")]
        public void AddAttributeToAssembly(string attribute, string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var testAssembly = ctx.GetOrCreateAssembly(assemblyName);
            testAssembly.AddAttribute(attribute);
        }

        [Given(@"I have specified (.+) platform for assembly (.+)")]
        public void DefinePaltformForAssembly(string platform, string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var testAssembly = ctx.GetOrCreateAssembly(assemblyName);
            testAssembly.Platform = platform.ConvertToPlatform();
        }        

        [Given(@"I have compiled the assembly (.+) to file (.+)")]
        public void Compile(string assemblyName, string assemblyFileName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            assemblyFileName = Path.Combine(ctx.SandboxPath, assemblyFileName);
            var testAssembly = ctx.GetOrCreateAssembly(assemblyName);
            var compiler = new Compiler();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            compiler.Compile(testAssembly, assemblyFileName, configuration.FrameworkVersion);            
        }

        [Given(@"I have created the folder (.+)")]
        public void CreateDirectory(string targetDirectoryName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            targetDirectoryName = Path.Combine(ctx.SandboxPath, targetDirectoryName);
            var environmentManager = new EnvironmentManager();
            environmentManager.CreateDirectory(targetDirectoryName);
        }

        [Given(@"I have copied NUNit framework assemblies to folder (.+)")]
        public void CopyNUnitFrameworkAssemblies(string targetDirectoryName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            targetDirectoryName = Path.Combine(ctx.SandboxPath, targetDirectoryName);
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            var environmentManager = new EnvironmentManager();
            environmentManager.CopyNUnitFrameworkAssemblies(targetDirectoryName, configuration.OriginNUnitPath, configuration.FrameworkVersion);
        }

        [Given(@"I have copied the reference (.+) to folder (.+)")]
        public void CopyReference(string referenceFileName, string targetDirectoryName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            targetDirectoryName = Path.GetFullPath(Path.Combine(ctx.SandboxPath, targetDirectoryName));
            referenceFileName = Path.GetFullPath(Path.Combine(ctx.SandboxPath, referenceFileName));
            var environmentManager = new EnvironmentManager();
            environmentManager.CopyReference(targetDirectoryName, referenceFileName);
        }

        [Given(@"I have copied NUnit framework references to folder (.+)")]
        public void CopyNUnitFrameworkReferences(string targetDirectoryName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            targetDirectoryName = Path.GetFullPath(Path.Combine(ctx.SandboxPath, targetDirectoryName));
            var environmentManager = new EnvironmentManager();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            foreach(var reference in environmentManager.EnumerateNUnitAssemblies(configuration.OriginNUnitPath, configuration.FrameworkVersion))
            {
                environmentManager.CopyReference(targetDirectoryName, reference);
            }
        }

        [Given(@"I have added config file (.+)")]
        public void AddConfigFile(string configFile)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();            
            configuration.AddConfigFile(new ConfigFile(Path.Combine(ctx.SandboxPath, configFile)));
        }

        [Given(@"I have added the reference (.+) to (.+)")]
        public void AddReference(string referenceFileName, string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            referenceFileName = Path.GetFullPath(Path.Combine(ctx.SandboxPath, referenceFileName));
            var assembly = ctx.GetOrCreateAssembly(assemblyName);
            assembly.AddReference(referenceFileName);
        }

        [Given(@"I have added NUnit framework references to (.+)")]
        public void AddNUnitFrameworkReference(string assemblyName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var assembly = ctx.GetOrCreateAssembly(assemblyName);            
            var environmentManager = new EnvironmentManager();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            foreach (var reference in environmentManager.EnumerateNUnitReferences(configuration.OriginNUnitPath, configuration.FrameworkVersion))
            {
                assembly.AddReference(reference);                
            }
        }

        [Given(@"I have added the assembly (.+) to the list of testing assemblies")]
        public void AddTestingAssembly(string assemblyFileName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            assemblyFileName = Path.Combine(ctx.SandboxPath, assemblyFileName);
            configuration.AddAssemblyFile(Path.GetFullPath(assemblyFileName));
        }

        [Given(@"I have added the arg ([^=]+\s*)=(\s*.+) to NUnit console command line")]
        public void AddArg(string arg, string value)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            var nUnitArg = arg.Trim().ConvertToNUnitArg();
            switch (nUnitArg)
            {
                case DataType.WorkingDirectory:
                    value = Path.Combine(ctx.SandboxPath, value);                    
                    break;
            }

            configuration.AddArg(string.IsNullOrEmpty(value) ? new CmdArg(nUnitArg) : new CmdArg(nUnitArg, value.Trim()));
        }

        [Given(@"I have added the arg ([^=]+) to NUnit console command line")]
        public void AddArg(string arg)
        {
            AddArg(arg, string.Empty);
        }
        
        [When(@"I run NUnit console")]
        public void RunNUnitConsole()
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            var runner = new NUnitRunner();
            ICommandLineSetupFactory setupFactory;
            switch (configuration.ConfigurationType)
            {
                case ConfigurationType.CmdArguments:
                    setupFactory = new ArgumentsCommandLineSetupFactory();
                    break;

                case ConfigurationType.ProjectFile:
                    setupFactory = new ProjectCommandLineSetupFactory();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            var testSession = runner.Run(ctx, setupFactory.Create(ctx));
            ctx.TestSession = testSession;            
        }

        [Then(@"processes (.+) are finished")]
        public void CheckProcessesWereFinished(string processName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var agents = (
                from process in ctx.TestSession.ProcessesAfter
                where process.ProcessName == processName
                select process).ToList();

            var agentsList = string.Join(", ", agents.Select(i => $"{i.Id}: {i.ProcessName}"));

            foreach (var agent in agents)
            {
                agent.Kill();                
            }

            Assert.IsEmpty(agents, $"Agents are not finished: {agentsList}.\nSee {ctx}");
        }

        [When(@"I remove (.+) from NUnit folder")]
        public void RemoveFileOrDirectoryFromNUnitDirectory(string fileToRemove)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var configuration = ctx.GetOrCreateNUnitConfiguration();
            var environmentManager = new EnvironmentManager();
            environmentManager.RemoveFileOrDirectoryFromNUnitDirectory(fileToRemove, configuration.NUnitConsolePath);
        }
     
        [Then(@"the exit code should be (-?\d+)")]
        public void VerifyExitCode(int expectedExitCode)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            Assert.AreEqual(expectedExitCode, ctx.TestSession.ExitCode, $"Invalid exit code.\nSee {ctx}");
        }
       
        [Then(@"the exit code should be negative")]
        public void VerifyExitCodeIsNegative()
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            Assert.Less(ctx.TestSession.ExitCode, 0, $"Invalid exit code.\nSee {ctx}");
        }

        [Then(@"the Test Run Summary should has following:")]
        public void VerifyTestRunSummary(Table dataTable)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            var testOutputSummaryParser = new TestOutputSummaryParser();

            var testOutputSummary = testOutputSummaryParser.Parse(ctx.TestSession.Output);
            var errorMessages = new List<string>();
            foreach (var row in dataTable.Rows)
            {
                var rowField = row["field"];
                var rowVal = row["value"];
                if (rowField == null || rowVal == null)
                {
                    continue;
                }

                string val;
                if (!testOutputSummary.TryGetValue(rowField, out val))
                {
                    errorMessages.Add($"Test Run Summary should containt field \"{rowField}\" = {rowVal}");
                    continue;
                }

                if (string.Equals(rowVal, val, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                errorMessages.Add($"Test Run Summary should containt field \"{rowField}\" = {rowVal}, but value is {val}");
            }

            if (errorMessages.Count > 0)
            {
                Assert.Fail($"See {ctx}\n{string.Join("\n", errorMessages)}");
            }
        }

        [Given(@"I have created assemblies according to (.+)")]
        public void CreateAssemblies(string xmlReportFileName)
        {
            var ctx = ScenarioContext.Current.GetTestContext();
            xmlReportFileName = Path.Combine(ctx.SandboxPath, xmlReportFileName);

            using (var xmlReportFile = File.OpenRead(xmlReportFileName))
            {
                var doc = XDocument.Load(xmlReportFile);

                var tests =
                    from assemblyElement in doc.XPathSelectElements("//test-suite[@type='Assembly' and @name]")
                    let assemblyName = Path.GetFileNameWithoutExtension(assemblyElement.Attribute("name").Value)
                    from testFixtureElement in assemblyElement.XPathSelectElements(".//test-suite[@type='TestFixture' and @name]")
                    let testFixtureName = testFixtureElement.Attribute("name").Value
                    let testFixtureNamespace = string.Join(".", GetNamespaces(testFixtureElement).Reverse())
                    let testFixtureFullName = string.Join(".", Enumerable.Repeat(testFixtureNamespace, 1).Concat(Enumerable.Repeat(testFixtureName, 1)).Where(i => !string.IsNullOrWhiteSpace(i)))
                    from testElement in testFixtureElement.XPathSelectElements(".//test-case[@name]")
                    let fullTestName = testElement.Attribute("name").Value
                    where fullTestName.Length > testFixtureFullName.Length + 1
                    let testName = fullTestName.Substring(testFixtureFullName.Length + 1, fullTestName.Length - testFixtureFullName.Length - 1)
                    let result = testElement.Attribute("result")?.Value ?? string.Empty
                    select new { testName, testFixtureNamespace, testFixtureFullName, assemblyName, result };

                foreach (var test in tests)
                {
                    switch (test.result.ToLowerInvariant())
                    {
                        case "success":
                            AddMethod("Successful", test.testName, test.testFixtureNamespace, test.testFixtureFullName, test.assemblyName);
                            break;

                        case "inconclusive":
                            AddMethod("Inconclusive", test.testName, test.testFixtureNamespace, test.testFixtureFullName, test.assemblyName);
                            break;

                        case "ignored":
                            AddMethod("Ignored", test.testName, test.testFixtureNamespace, test.testFixtureFullName, test.assemblyName);
                            break;

                        case "failed":
                            AddMethod("Failed", test.testName, test.testFixtureNamespace, test.testFixtureFullName, test.assemblyName);
                            break;
                    }
                }
            }
        }

        private static IEnumerable<string> GetNamespaces(XElement element)
        {
            if (element == null)
            {
                yield break;
            }

            if (element.Name == "test-suite" && StringComparer.InvariantCultureIgnoreCase.Equals(element.Attribute("type")?.Value ?? string.Empty, "Namespace"))
            {
                yield return element.Attribute("name")?.Value ?? string.Empty;
            }

            if (element.Parent == null)
            {
                yield break;
            }

            var parentNs = GetNamespaces(element.Parent).ToList();
            foreach (var parentNamespace in parentNs)
            {
                yield return parentNamespace;
            }
        }
    }
}
