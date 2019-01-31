﻿using LogJoint.Analytics;
using LogJoint.Analytics.Timeline;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using System.Threading;

namespace LogJoint.Postprocessing.Timeline
{
	public class TimelinePostprocessorOutput: ITimelinePostprocessorOutput
	{
		public TimelinePostprocessorOutput(XmlReader reader, ILogSource logSource, ILogPartTokenFactory rotatedLogPartFactory = null) :
			this(reader, logSource, TimelineEntitiesComparer.Instance, rotatedLogPartFactory)
		{ }

		public TimelinePostprocessorOutput(XmlReader reader, ILogSource logSource, IEntitiesComparer entitiesComparer, ILogPartTokenFactory rotatedLogPartFactory)
		{
			this.logSource = logSource;

			if (!reader.ReadToFollowing("root"))
				throw new FormatException();
			etag.Read(reader);

			var eventsDeserializer = new EventsDeserializer(TextLogEventTrigger.DeserializerFunction);
			var events = new List<Event>();
			foreach (var elt in reader.ReadChildrenElements())
			{
				if (eventsDeserializer.TryDeserialize(elt, out var evt))
					events.Add(evt);
				else if (rotatedLogPartFactory.TryReadLogPartToken(elt, out var tmp))
					this.rotatedLogPartToken = tmp;
			}
			this.timelineEvents = events.AsReadOnly();
		}

		public static Task SerializePostprocessorOutput(
			IEnumerableAsync<Event[]> events,
			Task<ILogPartToken> rotatedLogPartToken,
			Func<object, TextLogEventTrigger> triggersConverter,
			string contentsEtagAttr,
			string outputFileName,
			ITempFilesManager tempFiles,
			CancellationToken cancellation
		)
		{
			return events.SerializePostprocessorOutput<Event, EventsSerializer, IEventsVisitor>(
				triggerSerializer => new EventsSerializer(triggerSerializer),
				rotatedLogPartToken,
				triggersConverter,
				contentsEtagAttr,
				"root",
				outputFileName,
				tempFiles,
				cancellation
			);
		}


		ILogSource ITimelinePostprocessorOutput.LogSource { get { return logSource; } }

		IList<Event> ITimelinePostprocessorOutput.TimelineEvents { get { return timelineEvents; } }

		TimeSpan ITimelinePostprocessorOutput.TimelineOffset { get { return timelineOffset; } }
		void ITimelinePostprocessorOutput.SetTimelineOffset(TimeSpan value) { timelineOffset = value; }

		string ITimelinePostprocessorOutput.SequenceDiagramName { get { return sequenceDiagramName; } }
		void ITimelinePostprocessorOutput.SetSequenceDiagramName(string value) { sequenceDiagramName = value; }

		ILogPartToken ITimelinePostprocessorOutput.RotatedLogPartToken { get { return rotatedLogPartToken; } }

		string IPostprocessorOutputETag.ETag { get { return etag.Value; } }

		readonly ILogSource logSource;
		readonly ILogPartToken rotatedLogPartToken = new NullLogPartToken();
		readonly PostprocessorOutputETag etag;
		readonly IList<Event> timelineEvents;
		TimeSpan timelineOffset;
		string sequenceDiagramName;

		const string rotatedLogPartTokenEltName = "rotatedLogPartToken";
	};
}
