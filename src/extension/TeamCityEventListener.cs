﻿// ***********************************************************************
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

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Engine.Extensibility;

namespace NUnit.Engine.Listeners
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    // Note: Setting mimimum engine version in this case is
    // purely documentary since engines prior to 3.4 do not
    // check the EngineVersion property and will try to
    // load this extension anyway.
    [Extension(Enabled = false, EngineVersion = "3.4")]
    [SuppressMessage("ReSharper", "UseNameofExpression")]
    public class TeamCityEventListener : ITestEventListener
    {
        private static readonly ServiceMessageWriter ServiceMessageWriter = new ServiceMessageWriter();
        private readonly TeamCityMessageConverter _messageConverter = new TeamCityMessageConverter();
        private readonly TextWriter _outWriter;
        private readonly Dictionary<string, string> _refs = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _blockCounters = new Dictionary<string, int>();

        public TeamCityEventListener() : this(Console.Out) { }

        public TeamCityEventListener(TextWriter outWriter)
        {
            if (outWriter == null) throw new ArgumentNullException("outWriter");

            _outWriter = outWriter;
        }

        #region ITestEventListener Implementation

        public void OnTestEvent(string report)
        {
            var doc = new XmlDocument();
            doc.LoadXml(report);

            var testEvent = doc.FirstChild;

            TeamCityMessageConverter.NUnitMessage message;
            if (!TeamCityMessageConverter.NUnitMessage.TryParse(testEvent, out message))
            {
                return;
            }

            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                foreach (var tcMessage in _messageConverter.ConvertMessage(message))
                {
                    ServiceMessageWriter.Write(writer, tcMessage);
                }
            }

            _outWriter.WriteLine(sb.ToString());
        }

        #endregion

        private void CaseStartTest(string id, string flowId, string parentId, string testFlowId, string fullName)
        {
            if (id != flowId && parentId != null)
            {
                OnFlowStarted(id, flowId);
            }

            OnTestStart(testFlowId, fullName);
        }

        private void TestSuiteCase(string parentId, string flowId, string fullName)
        {
            // NUnit 3 case
            if (parentId == string.Empty)
            {
                OnRootSuiteFinish(flowId, fullName);
            }

            // NUnit 2 case
            if (parentId == null)
            {
                if (ChangeBlockCounter(flowId, -1) == 0)
                {
                    OnRootSuiteFinish(flowId, fullName);
                }
            }
        }

        private void StartSuiteCase(string parentId, string flowId, string fullName)
        {
            // NUnit 3 case
            if (parentId == string.Empty)
            {
                OnRootSuiteStart(flowId, fullName);
            }

            // NUnit 2 case
            if (parentId == null)
            {
                if (ChangeBlockCounter(flowId, 1) == 1)
                {
                    OnRootSuiteStart(flowId, fullName);
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

        private void TrySendOutput(string flowId, XmlNode message, string fullName)
        {
            if (message == null) throw new ArgumentNullException("message");

            var output = message.SelectSingleNode("output");
            if (output == null)
            {
                return;
            }

            var outputStr = output.InnerText;
            if (string.IsNullOrEmpty(outputStr))
            {
                return;
            }

            Write(new ServiceMessage(ServiceMessage.Names.TestStdOut, 
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Out, outputStr),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId),
                new ServiceMessageAttr(ServiceMessageAttr.Names.TcTags, "tc:parseServiceMessagesInside")));
        }        

        private void OnRootSuiteStart(string flowId, string assemblyName)
        {
            assemblyName = Path.GetFileName(assemblyName);
            
            Write(new ServiceMessage(ServiceMessage.Names.TestSuiteStarted,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, assemblyName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));
        }

        private void OnRootSuiteFinish(string flowId, string assemblyName)
        {
            assemblyName = Path.GetFileName(assemblyName);
            
            Write(new ServiceMessage(ServiceMessage.Names.TestSuiteFinished,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, assemblyName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));
        }

        private void OnFlowStarted(string flowId, string parentFlowId)
        {
            Write(new ServiceMessage(ServiceMessage.Names.FlowStarted,
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Parent, parentFlowId)));
        }

        private void OnFlowFinished(string flowId)
        {
            Write(new ServiceMessage(ServiceMessage.Names.FlowFinished,
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));
        }

        private void OnTestStart(string flowId, string fullName)
        {
            Write(new ServiceMessage(ServiceMessage.Names.TestStarted,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.CaptureStandardOutput, "false"),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));
        }

        private void OnTestFinished(string flowId, XmlNode message, string fullName)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var durationStr = message.GetAttribute(ServiceMessageAttr.Names.Duration);
            double durationDecimal;
            int durationMilliseconds = 0;
            if (durationStr != null && double.TryParse(durationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out durationDecimal))
            {
                durationMilliseconds = (int)(durationDecimal * 1000d);
            }

            TrySendOutput(flowId, message, fullName);

            Write(new ServiceMessage(ServiceMessage.Names.TestFinished,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Duration, durationMilliseconds.ToString()),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));
        }

        private void OnTestFailed(string flowId, XmlNode message, string fullName)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var errorMessage = message.SelectSingleNode("failure/message");
            var stackTrace = message.SelectSingleNode("failure/stack-trace");

            Write(new ServiceMessage(ServiceMessage.Names.TestFailed,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Message, errorMessage == null ? string.Empty : errorMessage.InnerText),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Details, stackTrace == null ? string.Empty : stackTrace.InnerText),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));

            OnTestFinished(flowId, message, fullName);
        }

        private void OnTestSkipped(string flowId, XmlNode message, string fullName)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            TrySendOutput(flowId, message, fullName);
            var reason = message.SelectSingleNode("reason/message");

            Write(new ServiceMessage(ServiceMessage.Names.TestIgnored,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Message, reason == null ? string.Empty : reason.InnerText),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));
        }

        private void OnTestInconclusive(string flowId, XmlNode message, string fullName)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            TrySendOutput(flowId, message, fullName);

            Write(new ServiceMessage(ServiceMessage.Names.TestIgnored,
                new ServiceMessageAttr(ServiceMessageAttr.Names.Name, fullName),
                new ServiceMessageAttr(ServiceMessageAttr.Names.Message, "Inconclusive"),
                new ServiceMessageAttr(ServiceMessageAttr.Names.FlowId, flowId)));            
        }

        
    }
}
