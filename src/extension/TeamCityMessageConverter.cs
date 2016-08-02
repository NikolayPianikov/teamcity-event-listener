// ***********************************************************************
// Copyright (c) 2015 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

namespace NUnit.Engine.Listeners
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    internal class TeamCityMessageConverter
    {
        private readonly Dictionary<string, string> _refs = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _blockCounters = new Dictionary<string, int>();

        public IEnumerable<ServiceMessage> ConvertMessage(NUnitMessage message)
        {
            if (message == null) throw new ArgumentNullException("message");

            if (message.Name.ToLowerInvariant() == "start-run")
            {
                _refs.Clear();
                yield break;
            }

            var flowId = ".";
            if (message.ParentId != null)
            {
                // NUnit 3 case
                string rootId;
                flowId = TryFindRootId(message.ParentId, out rootId) ? rootId : message.Id;
            }
            else
            {
                // NUnit 2 case
                if (!string.IsNullOrEmpty(message.Id))
                {
                    var idParts = message.Id.Split('-');
                    if (idParts.Length == 2)
                    {
                        flowId = idParts[0];
                    }
                }
            }

            string testFlowId;
            if (message.Id != flowId && message.ParentId != null)
            {
                testFlowId = message.Id;
            }
            else
            {
                testFlowId = flowId;
                if (testFlowId == null)
                {
                    testFlowId = message.Id;
                }
            }

            switch (message.Name.ToLowerInvariant())
            {
                case "start-suite":
                    _refs[message.Id] = message.ParentId;
                    foreach (var tcMessage in StartSuiteCase(message.ParentId, flowId, message.FullName))
                    {
                        yield return tcMessage;
                    }

                    break;

                case "test-suite":
                    _refs.Remove(message.Id);
                    foreach (var tcMessage in TestSuiteCase(message.ParentId, flowId, message.FullName))
                    {
                        yield return tcMessage;
                    }

                    break;

                case "start-test":
                    _refs[message.Id] = message.ParentId;
                    foreach (var tcMessage in CaseStartTest(message.Id, flowId, message.ParentId, testFlowId, message.FullName))
                    {
                        yield return tcMessage;
                    }

                    break;

                case "test-case":
                    if (!_refs.Remove(message.Id))
                    {
                        // When test without starting
                        foreach (var tcMessage in CaseStartTest(message.Id, flowId, message.ParentId, testFlowId, message.FullName))
                        {
                            yield return tcMessage;
                        }
                    }

                    if (string.IsNullOrEmpty(message.Result))
                    {
                        break;
                    }

                    switch (message.Result.ToLowerInvariant())
                    {
                        case "passed":
                            foreach (var tcMessage in OnTestFinished(testFlowId, message))
                            {
                                yield return tcMessage;
                            }

                            break;

                        case "inconclusive":
                            foreach (var tcMessage in OnTestInconclusive(testFlowId, message))
                            {
                                yield return tcMessage;
                            }

                            break;

                        case "skipped":
                            foreach (var tcMessage in OnTestSkipped(testFlowId, message))
                            {
                                yield return tcMessage;
                            }

                            break;

                        case "failed":
                            foreach (var tcMessage in OnTestFailed(testFlowId, message))
                            {
                                yield return tcMessage;
                            }

                            break;
                    }

                    if (message.Id != flowId && message.ParentId != null)
                    {
                        foreach (var tcMessage in OnFlowFinished(message.Id))
                        {
                            yield return tcMessage;
                        }
                    }

                    break;
            }
        }

        private IEnumerable<ServiceMessage> CaseStartTest(string id, string flowId, string parentId, string testFlowId, string fullName)
        {
            if (id != flowId && parentId != null)
            {
                foreach (var tcMessage in OnFlowStarted(id, flowId))
                {
                    yield return tcMessage;
                }
            }

            foreach (var tcMessage in OnTestStart(testFlowId, fullName))
            {
                yield return tcMessage;
            }
        }

        private IEnumerable<ServiceMessage> TestSuiteCase(string parentId, string flowId, string fullName)
        {
            // NUnit 3 case
            if (parentId == string.Empty)
            {
                foreach (var tcMessage in OnRootSuiteFinish(flowId, fullName))
                {
                    yield return tcMessage;
                }
            }

            // NUnit 2 case
            if (parentId == null)
            {
                if (ChangeBlockCounter(flowId, -1) == 0)
                {
                    foreach (var tcMessage in OnRootSuiteFinish(flowId, fullName))
                    {
                        yield return tcMessage;
                    }
                }
            }
        }

        private IEnumerable<ServiceMessage> StartSuiteCase(string parentId, string flowId, string fullName)
        {
            // NUnit 3 case
            if (parentId == string.Empty)
            {
                foreach (var tcMessage in OnRootSuiteStart(flowId, fullName))
                {
                    yield return tcMessage;
                }
            }

            // NUnit 2 case
            if (parentId == null)
            {
                if (ChangeBlockCounter(flowId, 1) == 1)
                {
                    foreach (var tcMessage in OnRootSuiteStart(flowId, fullName))
                    {
                        yield return tcMessage;
                    }
                }
            }
        }

        private int ChangeBlockCounter(string flowId, int changeValue)
        {
            int currentBlockCounter;
            if (!_blockCounters.TryGetValue(flowId, out currentBlockCounter))
            {
                currentBlockCounter = 0;
            }

            currentBlockCounter += changeValue;
            _blockCounters[flowId] = currentBlockCounter;
            return currentBlockCounter;
        }

        private bool TryFindParentId(string id, out string parentId)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            return _refs.TryGetValue(id, out parentId) && !string.IsNullOrEmpty(parentId);
        }

        private bool TryFindRootId(string id, out string rootId)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            while (TryFindParentId(id, out rootId) && id != rootId)
            {
                id = rootId;
            }

            rootId = id;
            return !string.IsNullOrEmpty(id);
        }

        private IEnumerable<ServiceMessage> TrySendOutput(string flowId, NUnitMessage message)
        {
            if (message == null) throw new ArgumentNullException("message");

            var output = message.Output;
            if (string.IsNullOrEmpty(output))
            {
                yield break;
            }

            yield return new ServiceMessage(ServiceMessage.Names.TestStdOut,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, message.FullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Out, output),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId),
                new ServiceMessageAttr(ServiceMessageAttr.Names.TcTags, "tc:parseServiceMessagesInside"));
        }

        private IEnumerable<ServiceMessage> OnRootSuiteStart(string flowId, string assemblyName)
        {
            assemblyName = Path.GetFileName(assemblyName);

            yield return new ServiceMessage(ServiceMessage.Names.TestSuiteStarted,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, assemblyName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        private IEnumerable<ServiceMessage> OnRootSuiteFinish(string flowId, string assemblyName)
        {
            assemblyName = Path.GetFileName(assemblyName);

            yield return new ServiceMessage(ServiceMessage.Names.TestSuiteFinished,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, assemblyName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        private IEnumerable<ServiceMessage> OnFlowStarted(string flowId, string parentFlowId)
        {
            yield return new ServiceMessage(ServiceMessage.Names.FlowStarted,
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Parent, parentFlowId));
        }

        private IEnumerable<ServiceMessage> OnFlowFinished(string flowId)
        {
            yield return new ServiceMessage(ServiceMessage.Names.FlowFinished,
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        private IEnumerable<ServiceMessage> OnTestStart(string flowId, string fullName)
        {
            yield return new ServiceMessage(ServiceMessage.Names.TestStarted,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.CaptureStandardOutput, "false"),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        private IEnumerable<ServiceMessage> OnTestFinished(string flowId, NUnitMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var durationStr = message.Duration;
            double durationDecimal;
            int durationMilliseconds = 0;
            if (durationStr != null && double.TryParse(durationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out durationDecimal))
            {
                durationMilliseconds = (int)(durationDecimal * 1000d);
            }

            foreach (var tcMessage in TrySendOutput(flowId, message))
            {
                yield return tcMessage;
            }

            yield return new ServiceMessage(ServiceMessage.Names.TestFinished,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, message.FullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Duration, durationMilliseconds.ToString()),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        private IEnumerable<ServiceMessage> OnTestFailed(string flowId, NUnitMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            yield return new ServiceMessage(ServiceMessage.Names.TestFailed,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, message.FullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Message, message.ErrorMessage),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Details, message.StackTrace),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));

            foreach (var tcMessage in OnTestFinished(flowId, message))
            {
                yield return tcMessage;
            }
        }

        private IEnumerable<ServiceMessage> OnTestSkipped(string flowId, NUnitMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            foreach (var tcMessage in TrySendOutput(flowId, message))
            {
                yield return tcMessage;
            }

            yield return new ServiceMessage(ServiceMessage.Names.TestIgnored,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, message.FullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Message, message.Message),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        private IEnumerable<ServiceMessage> OnTestInconclusive(string flowId, NUnitMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            foreach (var tcMessage in TrySendOutput(flowId, message))
            {
                yield return tcMessage;
            }

            yield return new ServiceMessage(ServiceMessage.Names.TestIgnored,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, message.FullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Message, "Inconclusive"),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId));
        }

        internal class NUnitMessage
        {
            private readonly XmlNode _testEvent;

            public NUnitMessage(XmlNode testEvent)
            {
                _testEvent = testEvent;
            }

            public static bool TryParse(XmlNode testEvent, out NUnitMessage message)
            {
                if (testEvent == null) throw new ArgumentNullException("testEvent");

                message = default(NUnitMessage);

                var name = testEvent.Name;
                if (string.IsNullOrEmpty(name))
                {
                    return false;
                }

                var fullName = testEvent.GetAttribute("fullname");
                if (string.IsNullOrEmpty(fullName))
                {
                    return false;
                }

                var id = testEvent.GetAttribute("id");
                if (id == null)
                {
                    id = string.Empty;
                }

                var parentId = testEvent.GetAttribute("parentId");

                message = NUnitMessage(id, parentId, name, fullName, result, output);
                return true;
            }

            public string Id {
                get
                {
                    return _testEvent.GetAttribute("id") ?? string.Empty;
                }
            }

            public string ParentId { get; private set; }

            public string Name { get; private set; }

            public string Result { get; private set; }

            public string Output { get; private set; }

            public string FullName { get; private set; }

            public string Duration { get; private set; }

            public string ErrorMessage { get; private set; }

            public string StackTrace { get; private set; }

            public string Message { get; private set; }

            public bool Validate()
            {
                return !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(FullName) && Id != null;
            }
        }
    }
}