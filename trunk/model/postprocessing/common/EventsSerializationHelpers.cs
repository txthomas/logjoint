﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using LogJoint.Postprocessing;

namespace LogJoint.Analytics
{
	internal static class EventsSerializationHelpers
	{
		public static async Task SerializePostprocessorOutput<Evt, Serializer, EvtVisitor>(
			this IEnumerableAsync<Evt[]> events,
			Func<Action<object, XElement>, Serializer> serializerFactory,
			Task<ILogPartToken> rotatedLogPartToken,
			Func<object, TextLogEventTrigger> triggersConverter,
			string contentsEtagAttr,
			string rootElementName,
			string outputFileName,
			ITempFilesManager tempFiles,
			CancellationToken cancellation
		) where Evt : IVisitable<EvtVisitor> where Serializer : class, IEventsSerializer, EvtVisitor
		{
			rotatedLogPartToken = rotatedLogPartToken ?? Task.FromResult<ILogPartToken>(null);
			var sortKeyAttr = XName.Get("__key");
			var chunks = new List<string>();
			Serializer serializer = null;
			Action resetSerializer = () =>
			{
				if (serializer?.Output?.Count > 0)
				{
					string chunkFileName = tempFiles.GenerateNewName();
					chunks.Add(chunkFileName);
					using (var writer = XmlWriter.Create(chunkFileName, new XmlWriterSettings()
					{
						OmitXmlDeclaration = true,
						ConformanceLevel = ConformanceLevel.Fragment
					}))
					{
						foreach (var e in serializer.Output.OrderBy(e => e.AttributeValue(sortKeyAttr)))
							e.WriteTo(writer);
					}
				}
				serializer = serializerFactory((trigger, elt) =>
				{
					triggersConverter(trigger).Save(elt);
					elt.SetAttributeValue(sortKeyAttr, ((IOrderedTrigger)trigger).Index.ToString("x8"));
				});
			};
			resetSerializer();
			await events.ForEach(batch =>
			{
				foreach (var e in batch)
				{
					e.Visit(serializer);
					if (serializer.Output.Count >= 8 * 1024)
						resetSerializer();
				}
				return Task.FromResult(!cancellation.IsCancellationRequested);
			});
			resetSerializer();

			if (cancellation.IsCancellationRequested)
				return;

			using (var outputWriter = XmlWriter.Create(outputFileName, new XmlWriterSettings()
			{
				Indent = true
			}))
			{
				outputWriter.WriteStartElement(rootElementName);
				new PostprocessorOutputETag(contentsEtagAttr).Write(outputWriter);
				(await rotatedLogPartToken).SafeWriteTo(outputWriter);
				var readersSettings = new XmlReaderSettings()
				{
					ConformanceLevel = ConformanceLevel.Fragment
				};
				var readers = chunks.Select(chunkFileName => XmlReader.Create(chunkFileName, readersSettings)).ToList();
				try
				{
					var q = new VCSKicksCollection.PriorityQueue<KeyValuePair<XmlReader, XElement>>(new ListUtils.LambdaComparer<KeyValuePair<XmlReader, XElement>>((item1, item2) =>
					{
						return string.CompareOrdinal(item1.Value.AttributeValue(sortKeyAttr), item2.Value.AttributeValue(sortKeyAttr));
					}));
					Action<XmlReader> enqueueReader = reader =>
					{
						if (!reader.EOF)
						{
							if (reader.MoveToContent() != XmlNodeType.Element)
								throw new InvalidOperationException("bad chunk");
							q.Enqueue(new KeyValuePair<XmlReader, XElement>(reader, (XElement)XNode.ReadFrom(reader)));
						}
					};
					readers.ForEach(enqueueReader);
					while (q.Count > 0)
					{
						var item = q.Dequeue();
						item.Value.Attribute(sortKeyAttr).Remove();
						item.Value.WriteTo(outputWriter);
						enqueueReader(item.Key);
					}
				}
				finally
				{
					readers.ForEach(r => r.Dispose());
					chunks.ForEach(chunkFileName => tempFiles.DeleteIfTemporary(chunkFileName));
				}
				outputWriter.WriteEndElement(); // end of root node
			}
		}
	};
}
