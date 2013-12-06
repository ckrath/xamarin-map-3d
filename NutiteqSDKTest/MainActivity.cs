using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Com.Nutiteq;
using Com.Nutiteq.Projections;
using NutiteqComponents;
using NutiteqStyle;
using NutiteqGeometry;
using Com.Nutiteq.Utils;
using Com.Nutiteq.UI;
using Android.Graphics;
using Com.Nutiteq.Log;
using Com.Nutiteq.Layers.Vector;
using Com.Nutiteq.Vectorlayers;
using Com.Nutiteq.Roofs;
using Com.Nutiteq.Layers.Raster;
using System.Runtime.InteropServices;
using jsqlite;
using Java.IO;

namespace NutiteqSDKTest
{

	[Activity (Label = "NutiteqSDKTest", MainLauncher = true)]
	public class MainActivity : Activity
	{

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// enable Nutiteq SDK logging

			Log.EnableAll ();

			// get MapView

			MapView view = FindViewById<MapView> (Resource.Id.mapView);

			// define mandatory parameters

			// Components keeps internal state and parameters for MapView
			view.Components = new Components ();

			// define base projection, almost always EPSG3857, but others can be defined also
			EPSG3857 proj = new EPSG3857 ();


			// set online base layer with MapQuest Open Tiles
			view.Layers.BaseLayer = new TMSMapLayer(proj, 0, 18, 0, "http://otile1.mqcdn.com/tiles/1.0.0/osm/", "/", ".png");

			/*
			//set offline base layer from MBTiles file
			//TODO: set path properly
			String MbTilePath = "/sdcard/europe-tilemill-mbtiles.sqlite";

			view.Layers.BaseLayer = new MBTilesMapLayer (proj, 0, 5, 1, MbTilePath, this);
			*/


			// some map configuration
			Options mapOptions = view.Options;
			// sky conf
			mapOptions.SkyDrawMode  = Options.DrawBitmap;
			mapOptions.SkyOffset = 4.86f;
			Bitmap skyBitmap = UnscaledBitmapLoader.DecodeResource(Resources, Resource.Drawable.sky_small);
			mapOptions.SetSkyBitmap (skyBitmap);

			// make map loading faster with more workers
			mapOptions.RasterTaskPoolSize = 4;

			// start map
			view.StartMapping ();

			// add a marker

			// define marker style (image, size, color)
			Bitmap pointMarker = UnscaledBitmapLoader.DecodeResource(Resources, Resource.Drawable.olmarker);

			MarkerStyle.Builder markerStyleBuilder = new MarkerStyle.Builder ();
			markerStyleBuilder.SetBitmap (pointMarker);
			markerStyleBuilder.SetColor (NutiteqComponents.Color.White);
			markerStyleBuilder.SetSize (0.5f);
			markerStyleBuilder.SetOffset2DX (0.5f);
			MarkerStyle markerStyle = markerStyleBuilder.Build ();

			// define label what is shown when you click on marker
			Label markerLabel = new DefaultLabel ("San Francisco", "Here is a marker");

			// define location of the marker, it must be converted to base map coordinate system
			MapPos SanFrancisco = view.Layers.BaseLayer.Projection.FromWgs84 (-122.416667f, 37.766667f);
			MapPos London = view.Layers.BaseLayer.Projection.FromWgs84 (0.0f, 51.0f);
			MapPos Tallinn = view.Layers.BaseLayer.Projection.FromWgs84 (24.74f, 54.43f);

			// create layer and add object to the layer, finally add layer to the map. 
			// All overlay layers must be same projection as base layer, so we reuse it
			MarkerLayer markerLayer = new MarkerLayer(view.Layers.BaseLayer.Projection);

			markerLayer.Add(new Marker(SanFrancisco, markerLabel, markerStyle, markerLayer));
			view.Layers.AddLayer(markerLayer);

			// 3d building layer

			Polygon3DStyle.Builder nml3dStyleBuilder = new Polygon3DStyle.Builder ();
			Polygon3DStyle nml3dStyle = nml3dStyleBuilder.Build ();

			StyleSet nmlStyleSet = new StyleSet ();
			nmlStyleSet.SetZoomStyle (14, nml3dStyle);

			NMLModelOnlineLayer Online3dLayer = new NMLModelOnlineLayer (view.Layers.BaseLayer.Projection, "http://aws-lb.nutiteq.ee/nml/nmlserver2.php?data=demo&", nmlStyleSet);

			// persistent caching settings for the NML layer
			Online3dLayer.SetMemoryLimit (20*1024*1024); // 20 MB
			Online3dLayer.SetPersistentCacheSize (50*1024*1024); // 50 MB
			Online3dLayer.SetPersistentCachePath ("/sdcard/nmlcache.db"); // mandatory to be set

			view.Layers.AddLayer(Online3dLayer);

			// Spatialite query, show results on map
			// 1. create style and layer for data

			LineStyle.Builder lineStyleBuilder = new LineStyle.Builder ();
			lineStyleBuilder.SetColor (NutiteqComponents.Color.Argb(0xff, 0x5C, 0x40, 0x33)); //brown
			lineStyleBuilder.SetWidth (0.05f);
			LineStyle lineStyle = lineStyleBuilder.Build ();

			GeometryLayer geomLayer = new GeometryLayer (view.Layers.BaseLayer.Projection);
			view.Layers.AddLayer (geomLayer);

			// 2. do the query, pass results to the layer
			Database db = new Database ();

			try {
				db.Open ("/sdcard/mapxt/estonia-latest-map.sqlite", Constants.SqliteOpenReadonly);

				// show versions to verify that modules are there
				db.Exec ("SELECT spatialite_version(), proj4_version(), geos_version(), sqlite_version()", new GeneralQryResult ());

				// real spatial query. Limit to 1000 objects to avoid layer overloading
				String qry = "SELECT id, HEX(AsBinary(Transform(geometry,3857))), sub_type, name FROM ln_railway LIMIT 1000";
				db.Exec (qry, new SpatialQryResult (geomLayer, lineStyle));
			} catch (jsqlite.Exception ex) {
				Log.Error( ex.LocalizedMessage );
			}


			// OSM Polygon3D layer

			Polygon3DStyle.Builder poly3dStyleBuilder = new Polygon3DStyle.Builder ();
			poly3dStyleBuilder.SetColor (NutiteqComponents.Color.White);
			Polygon3DStyle poly3dStyle = poly3dStyleBuilder.Build ();

			StyleSet polyStyleSet = new StyleSet ();
			polyStyleSet.SetZoomStyle (16, poly3dStyle);

			Roof DefaultRoof = new FlatRoof ();

			Polygon3DOSMLayer Poly3DLayer = new Polygon3DOSMLayer (view.Layers.BaseLayer.Projection, 0.3f, DefaultRoof, unchecked((int) 0xffffffff) /* white */, unchecked((int) 0xff888888) /* gray */, 1500, polyStyleSet);
			view.Layers.AddLayer (Poly3DLayer);

			// set map center and zoom
			view.FocusPoint = Tallinn;
			view.Zoom = 5.0f;

			// set listener for map events
			MapListener listener = new MyMapListener ();
			view.Options.MapListener = listener;

		}

	}


	// adds query results to given layer, with given style
	public class SpatialQryResult : Java.Lang.Object, ICallback
	{

		GeometryLayer _geomLayer;
		Style _geomStyle;

		public SpatialQryResult(GeometryLayer geomLayer, Style geomStyle){
			_geomLayer = geomLayer;
			_geomStyle = geomStyle;
		}

		public bool Newrow (string[] rowdata)
		{

			string id = rowdata [0];
			string geomHex = rowdata [1];
			string type = rowdata [2];
			string name = rowdata [3];

			Label label;
			if (name != null && name.Length > 1) {
				label = new DefaultLabel (name, type);
			} else {
				label = null;
			}

			Geometry[] lineGeoms = WkbRead.ReadWkb(new ByteArrayInputStream(Utils
				.HexStringToByteArray(geomHex)), rowdata);

			// following fails if not Line, change for other geometries
			foreach (Line lineGeom in lineGeoms) {
				_geomLayer.Add(new Line(lineGeom.VertexList, label, (LineStyle)_geomStyle, _geomLayer));
			}

			return false;
		}

		public void Types (string[] types)
		{
			// never called really
		}

		public void Columns (string[] cols){
			Log.Debug ("Query result:");
			string row = "";
			foreach (var col in cols) {
				row += col + " | ";
			}
			Log.Info (row);
		}
	}

	// prints query results as text
	public class GeneralQryResult : Java.Lang.Object, ICallback
	{

		public bool Newrow (string[] rowdata)
		{
			string row = "";
			foreach (var data in rowdata) {
				row += data + " | ";
			}

			Log.Info(row);
			return false;
		}

		public void Types (string[] types)
		{
			// never called really
		}

		public void Columns (string[] cols){
			Log.Debug ("Query result:");
			string row = "";
			foreach (var col in cols) {
				row += col + " | ";
			}
			Log.Info (row);
		}
	}
}


