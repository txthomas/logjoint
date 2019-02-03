﻿using System.Threading.Tasks;
using System.Threading;
using System.Xml.Linq;
using System.Linq;
using LogJoint.Postprocessing;
using LogJoint.Analytics;
using CD = LogJoint.Chromium.ChromeDriver;
using CDL = LogJoint.Chromium.ChromeDebugLog;
using Sym = LogJoint.Symphony.Rtc;
using HAR = LogJoint.Chromium.HttpArchive;
using LogJoint.Analytics.Timeline;
using LogJoint.Postprocessing.Timeline;
using System.Xml;

namespace LogJoint.Chromium.Timeline
{
	public interface IPostprocessorsFactory
	{
		ILogSourcePostprocessor CreateChromeDriverPostprocessor();
		ILogSourcePostprocessor CreateChromeDebugPostprocessor();
		ILogSourcePostprocessor CreateHttpArchivePostprocessor();
		ILogSourcePostprocessor CreateSymRtcPostprocessor();
	};

	public class PostprocessorsFactory : IPostprocessorsFactory
	{
		readonly static string typeId = PostprocessorIds.Timeline;
		readonly static string caption = PostprocessorIds.Timeline;
		readonly ITempFilesManager tempFiles;

		public PostprocessorsFactory(ITempFilesManager tempFiles)
		{
			this.tempFiles = tempFiles;
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateChromeDriverPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption, 
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForChromeDriver(new CD.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), 
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker, 
					i.InputContentsEtag, tempFiles)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateChromeDebugPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption, 
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForChromeDebug(new CDL.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), 
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker, 
					i.InputContentsEtag, tempFiles)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateHttpArchivePostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption,
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForHttpArchive(new HAR.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), 
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker, 
					i.InputContentsEtag, tempFiles)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateSymRtcPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				typeId, caption,
				(doc, logSource) => DeserializeOutput(doc, logSource),
				i => RunForSymLog(new Sym.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler),
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker, 
					i.InputContentsEtag, tempFiles)
			);
		}

		ITimelinePostprocessorOutput DeserializeOutput(XmlReader data, ILogSource forSource)
		{
			return new TimelinePostprocessorOutput(data, forSource, null);
		}

		static IEnumerableAsync<Event[]> TrackTemplates(IEnumerableAsync<Event[]>  events, ICodepathTracker codepathTracker)
		{
			return events.Select(batch =>
			{
				if (codepathTracker != null)
					foreach (var e in batch)
						codepathTracker.RegisterUsage(e.TemplateId);
				return batch;
			});
		}

		async static Task RunForChromeDriver(
			IEnumerableAsync<CD.Message[]> input,
			string outputFileName, 
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr,
			ITempFilesManager tempFiles
		)
		{
			IPrefixMatcher matcher = new PrefixMatcher();
			var logMessages = CD.Helpers.MatchPrefixes(input, matcher).Multiplex();

			CD.ITimelineEvents networkEvents = new CD.TimelineEvents(matcher);
			Sym.ICITimelineEvents symCIEvents = new Sym.CITimelineEvents(matcher);
			var endOfTimelineEventSource = new GenericEndOfTimelineEventSource<CD.MessagePrefixesPair>(m => m.Message);

			var networkEvts = networkEvents.GetEvents(logMessages);
			var eofEvts = endOfTimelineEventSource.GetEvents(logMessages);
			var symCIEvts = symCIEvents.GetEvents(logMessages);

			matcher.Freeze();

			var events = TrackTemplates(EnumerableAsync.Merge(
				networkEvts,
				eofEvts,
				symCIEvts
			), templatesTracker);

			var serialize = TimelinePostprocessorOutput.SerializePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.Make((CD.Message)evtTrigger),
				contentsEtagAttr,
				outputFileName,
				tempFiles,
				cancellation
			);

			await Task.WhenAll(serialize, logMessages.Open());
		}

		private static IEnumerableAsync<Event[]> RunForSymMessages(
			IPrefixMatcher matcher,
			IEnumerableAsync<Sym.Message[]> messages,
			ICodepathTracker templatesTracker,
			out IMultiplexingEnumerable<Sym.MessagePrefixesPair[]> symLog
		)
		{
			Sym.IMeetingsStateInspector symMeetingsStateInsector = new Sym.MeetingsStateInspector(matcher);
			Sym.IMediaStateInspector symMediaStateInsector = new Sym.MediaStateInspector(matcher);
			Sym.ITimelineEvents symTimelineEvents = new Sym.TimelineEvents(matcher);

			symLog = Sym.Helpers.MatchPrefixes(messages, matcher).Multiplex();
			var symMeetingStateEvents = symMeetingsStateInsector.GetEvents(symLog);
			var symMediaStateEvents = symMediaStateInsector.GetEvents(symLog);

			var symMeetingEvents = (new InspectedObjectsLifetimeEventsSource(e =>
			    e.ObjectType == Sym.MeetingsStateInspector.MeetingTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.MeetingSessionTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.MeetingRemoteParticipantTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.ProbeSessionTypeInfo
			)).GetEvents(symMeetingStateEvents);

			var symMediaEvents = (new InspectedObjectsLifetimeEventsSource(e =>
			   	e.ObjectType == Sym.MediaStateInspector.LocalScreenTypeInfo
			 || e.ObjectType == Sym.MediaStateInspector.LocalAudioTypeInfo
			 || e.ObjectType == Sym.MediaStateInspector.LocalVideoTypeInfo
			)).GetEvents(symMediaStateEvents);

			var events = TrackTemplates(EnumerableAsync.Merge(
				symMeetingEvents,
				symMediaEvents,
				symTimelineEvents.GetEvents(symLog)
			), templatesTracker);

			return events;
		}

		async static Task RunForChromeDebug(
			IEnumerableAsync<CDL.Message[]> input,
			string outputFileName, 
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr,
			ITempFilesManager tempFiles
		)
		{
			var multiplexedInput = input.Multiplex();
			IPrefixMatcher matcher = new PrefixMatcher();
			Sym.ICITimelineEvents symCI = new Sym.CITimelineEvents(matcher);

			var symEvents = RunForSymMessages(
				matcher,
				(new Sym.Reader()).FromChromeDebugLog(multiplexedInput),
				templatesTracker,
				out var symLog
			);
			var ciEvents = symCI.GetEvents(multiplexedInput);

			var events = EnumerableAsync.Merge(
				symEvents,
				ciEvents
			);

			matcher.Freeze();

			var serialize = TimelinePostprocessorOutput.SerializePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.FromUnknownTrigger(evtTrigger),
				contentsEtagAttr,
				outputFileName,
				tempFiles,
				cancellation
			);

			await Task.WhenAll(serialize, symLog.Open(), multiplexedInput.Open());
		}

		async static Task RunForHttpArchive(
			IEnumerableAsync<HAR.Message[]> input,
			string outputFileName, 
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr,
			ITempFilesManager tempFiles
		)
		{
			HAR.ITimelineEvents timelineEvents = new HAR.TimelineEvents();

			var events = TrackTemplates(EnumerableAsync.Merge(
				timelineEvents.GetEvents(input)
			), templatesTracker);

			await TimelinePostprocessorOutput.SerializePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.Make((HAR.Message)evtTrigger),
				contentsEtagAttr,
				outputFileName,
				tempFiles,
				cancellation
			);
		}

		async static Task RunForSymLog(
			IEnumerableAsync<Sym.Message[]> input,
			string outputFileName,
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr,
			ITempFilesManager tempFiles
		)
		{
			IPrefixMatcher matcher = new PrefixMatcher();
			var events = RunForSymMessages(matcher, input, templatesTracker, out var symLog);
			matcher.Freeze();

			var serialize = TimelinePostprocessorOutput.SerializePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.Make((Sym.Message)evtTrigger),
				contentsEtagAttr,
				outputFileName,
				tempFiles,
				cancellation
			);

			await Task.WhenAll(serialize, symLog.Open());
		}
	};
}
