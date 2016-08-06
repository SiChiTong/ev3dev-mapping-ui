﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(Transform))]
public class Map3D : MonoBehaviour
{
	public PointCloud mapPointCloud;

	private List<PointCloud> mapPointClouds=new List<PointCloud>();

	public int AssignVertices(Vector3[] data, int i_from, int len, bool[] is_invalid)
	{
		PointCloud pc;
		if (mapPointClouds.Count == 0 || mapPointClouds[mapPointClouds.Count - 1].UnassignedCount() < len)
		{			
			pc = Instantiate<PointCloud>(mapPointCloud);
			pc.transform.parent = transform;
			mapPointClouds.Add(pc);
		}
		else
			pc = mapPointClouds[mapPointClouds.Count - 1];

		return pc.AssignVertices(data, i_from, len, is_invalid);
	}

	public int VertexCount()
	{
		int v = 0;

		foreach (PointCloud pc in mapPointClouds)
			v += pc.AssignedCount();

		return v;
	}

	public void SaveToPlyPolygonFileFormat(string filename, string comment)
	{
		BinaryWriter bw=new BinaryWriter(File.Open(filename, FileMode.OpenOrCreate));

		PLYPolygonFileFormat.EmitHeader(bw, VertexCount(), comment);

		foreach (PointCloud pc in mapPointClouds)
			pc.SaveToPlyPolygonFileFormat(bw);

		bw.Close();
	}
		
	// Use this for initialization
	void Start ()
	{
		transform.parent = SceneManager.DynamicObjects;
	}
	
	// Update is called once per frame
	void Update ()
	{
	}
}