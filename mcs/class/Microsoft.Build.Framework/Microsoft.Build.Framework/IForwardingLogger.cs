using System;


namespace Microsoft.Build.Framework
{
	public interface IForwardingLogger : INodeLogger, ILogger
	{
		IEventRedirector BuildEventRedirector { get; set; }
		int NodeId { get; set; }
	}
}


