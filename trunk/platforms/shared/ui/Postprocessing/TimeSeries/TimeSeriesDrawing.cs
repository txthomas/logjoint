﻿using System;
using LJD = LogJoint.Drawing;
using System.Drawing;
using System.Linq;
using LogJoint.UI.Presenters.Postprocessing.TimelineVisualizer;
using LogJoint.Drawing;
using LogJoint.Postprocessing.Timeline;
using LogJoint.UI.Presenters.Postprocessing.TimeSeriesVisualizer;
using System.Collections.Generic;

namespace LogJoint.UI.Postprocessing.TimeSeriesVisualizer
{
	public static class Drawing
	{
		public class Resources
		{
			public readonly LJD.Pen GridPen = new LJD.Pen(Color.LightGray, 1);
			public LJD.Pen GetTimeSeriesPen(ModelColor color)
			{
				LJD.Pen ret;
				if (!pensCache.TryGetValue(color, out ret))
					pensCache.Add(color, ret = new LJD.Pen(color.ToColor(), 1));
				return ret;
			}
			public readonly LJD.Font AxesFont;
			public readonly float MajorAxisMarkSize = 5;
			public readonly float MinorAxisMarkSize = 3;
			public readonly float YAxesPadding = 6;

			public Resources(string fontName, float fontBaseSize)
			{
				AxesFont = new LJD.Font(fontName, fontBaseSize);
			}



			private readonly Dictionary<ModelColor, LJD.Pen> pensCache = new Dictionary<ModelColor, LJD.Pen>();
		};

		public static void DrawPlotsArea(
			LJD.Graphics g, 
			Resources resources, 
			PlotsDrawingData pdd, 
			PlotsViewMetrics m
		)
		{
			g.PushState();
			g.EnableAntialiasing(true);
			foreach (var s in pdd.TimeSeries)
			{
				var pen = resources.GetTimeSeriesPen(s.Color);
				var pts = s.Points.ToArray(); // todo: avoid array allocation. use DrawLine().
				if (pts.Length > 1)
					g.DrawLines(pen, pts);
				foreach (var p in pts)
					DrawPlotMarker(g, pen, p, s.Marker);
			}
			foreach (var x in pdd.XAxis.Points)
				g.DrawLine(resources.GridPen, new PointF(x.Position, 0), new PointF(x.Position, m.Size.Height));
			g.PopState();
		}

		public static void DrawLegendSample(
			LJD.Graphics g,
			Resources resources,
			ModelColor color,
			MarkerType markerType,
			RectangleF rect
		)
		{
			g.PushState();
			g.EnableAntialiasing(true);
			var pen = resources.GetTimeSeriesPen(color);
			var midX = (rect.X + rect.Right) / 2;
			var midY = (rect.Y + rect.Bottom) / 2;
			g.DrawLine(pen, rect.X, midY, rect.Right, midY);
			DrawPlotMarker(g, pen, new PointF(midX, midY), markerType);
			g.PopState();
		}

		static void DrawPlotMarker(LJD.Graphics g, LJD.Pen pen, PointF p, MarkerType markerType)
		{
			float markerSize = 3; // todo: calc size on given platform
			switch (markerType)
			{
				case MarkerType.Cross:
					g.DrawLine(pen, new PointF(p.X - markerSize, p.Y - markerSize), new PointF(p.X + markerSize, p.Y + markerSize));
					g.DrawLine(pen, new PointF(p.X - markerSize, p.Y + markerSize), new PointF(p.X + markerSize, p.Y - markerSize));
					break;
				case MarkerType.Circle:
					g.DrawEllipse(pen, new RectangleF(p.X - markerSize, p.Y - markerSize, markerSize * 2, markerSize * 2));
					break;
				case MarkerType.Square:
					g.DrawRectangle(pen, new RectangleF(p.X - markerSize, p.Y - markerSize, markerSize * 2, markerSize * 2));
					break;
				case MarkerType.Diamond:
					g.DrawLines(pen, new[] 
					{
						new PointF(p.X - markerSize, p.Y),
						new PointF(p.X, p.Y - markerSize),
						new PointF(p.X + markerSize, p.Y),
						new PointF(p.X, p.Y + markerSize),
						new PointF(p.X - markerSize, p.Y),
					});
					break;
				case MarkerType.Triangle:
					g.DrawLines(pen, new[]
					{
						new PointF(p.X - markerSize, p.Y + markerSize/2),
						new PointF(p.X, p.Y - markerSize),
						new PointF(p.X + markerSize, p.Y + markerSize/2),
						new PointF(p.X - markerSize, p.Y + markerSize/2),
					});
					break;
				case MarkerType.Plus:
					g.DrawLine(pen, new PointF(p.X - markerSize, p.Y), new PointF(p.X + markerSize, p.Y));
					g.DrawLine(pen, new PointF(p.X, p.Y - markerSize), new PointF(p.X, p.Y + markerSize));
					break;
				case MarkerType.Star:
					// plus
					g.DrawLine(pen, new PointF(p.X - markerSize, p.Y), new PointF(p.X + markerSize, p.Y));
					g.DrawLine(pen, new PointF(p.X, p.Y - markerSize), new PointF(p.X, p.Y + markerSize));
					// cross
					g.DrawLine(pen, new PointF(p.X - markerSize, p.Y - markerSize), new PointF(p.X + markerSize, p.Y + markerSize));
					g.DrawLine(pen, new PointF(p.X - markerSize, p.Y + markerSize), new PointF(p.X + markerSize, p.Y - markerSize));
					break;
			}
		}

		public static void DrawXAxis(LJD.Graphics g, Resources resources, PlotsDrawingData pdd, float height)
		{
			var pen = new LJD.Pen(Color.DarkGray, 1); // todo: make these all part of resources class
			var sb = new LJD.StringFormat(StringAlignment.Center, StringAlignment.Far);
			var majorMarkerHeight = (height - g.MeasureString("123", resources.AxesFont).Height) * 3f / 4f;
			var minorMarkerHeight = majorMarkerHeight / 2f;

			foreach (var x in pdd.XAxis.Points)
			{
				g.DrawLine(
					pen,
					new PointF(x.Position, 0),
					new PointF(x.Position, x.IsMajorMark ? majorMarkerHeight : minorMarkerHeight)
				);
				g.DrawString(x.Label, resources.AxesFont, LJD.Brushes.Black, new PointF(x.Position, height), sb);
			}
		}

		public static void DrawYAxes(LJD.Graphics g, Resources resources, PlotsDrawingData pdd, float yAxesAreaWidth, PlotsViewMetrics m)
		{
			float x = yAxesAreaWidth;
			var sf = new LJD.StringFormat(StringAlignment.Far, StringAlignment.Center); // todo: -> resources
			var sf2 = new LJD.StringFormat(StringAlignment.Center, StringAlignment.Far); // todo: -> resources
			var font = resources.AxesFont;
			foreach (var axis in pdd.YAxes)
			{
				float maxLabelWidth = 0;
				foreach (var p in axis.Points)
				{
					var pt = new PointF(x - (p.IsMajorMark ? resources.MajorAxisMarkSize : resources.MinorAxisMarkSize), p.Position);
					g.DrawLine(LJD.Pens.DarkGray, pt, new PointF(x, p.Position));
					if (p.Label != null)
						g.DrawString(p.Label, font, LJD.Brushes.Black /* to resources */, pt, sf);
					maxLabelWidth = Math.Max(maxLabelWidth, g.MeasureString(p.Label, resources.AxesFont).Width);
				}
				x -= (resources.MajorAxisMarkSize + maxLabelWidth);
				g.PushState();
				g.TranslateTransform(x, m.Size.Height / 2);
				g.RotateTransform(-90);
				g.DrawString(axis.Label, resources.AxesFont, LJD.Brushes.Black, new PointF(0, 0), sf2);
				g.PopState();
				x -= (g.MeasureString(axis.Label, resources.AxesFont).Height + resources.YAxesPadding);
			}
		}

		public static string GetYAxisId(LJD.Graphics g, Resources resources, PlotsDrawingData pdd, float xCoordinate, float viewWidth)
		{
			float x = viewWidth;
			foreach (var a in GetYAxesMetrics(g, resources, pdd))
			{
				var x2 = x - a.Width;
				if (xCoordinate > x2)
					return a.AxisData.Id;
				x = x2;
			}
			return null;
		}

		public struct YAxisMetrics
		{
			public AxisDrawingData AxisData;
			public float Width;
		};

		public static IEnumerable<YAxisMetrics> GetYAxesMetrics(LJD.Graphics g, Resources resources, PlotsDrawingData pdd)
		{
			foreach (var axis in pdd.YAxes)
			{
				float maxLabelWidth = 0;
				foreach (var p in axis.Points)
					maxLabelWidth = Math.Max(maxLabelWidth, g.MeasureString(p.Label, resources.AxesFont).Width);
				float unitTextHeight = g.MeasureString(axis.Label, resources.AxesFont).Height;
				yield return new YAxisMetrics()
				{
					AxisData = axis,
					Width = resources.MajorAxisMarkSize + maxLabelWidth + unitTextHeight + resources.YAxesPadding
				};
			}
		}
	}
}