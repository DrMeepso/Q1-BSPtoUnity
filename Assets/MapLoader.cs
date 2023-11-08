using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Windows;

struct bsp_Lump
{

    public UInt32 offset;
    public UInt32 length;
    public string name;

}

struct bsp_Header
{

    public UInt32 magic;

    public Dictionary<string, bsp_Lump> lumps;

};

struct bsp_Face
{

    public UInt16 plane;
    public UInt16 plane_side;
    public UInt32 first_edge;
    public UInt16 num_edges;
    public UInt16 tex_info;
    public UInt16 styles;
    public UInt32 light_offset;

    public bsp_Edge[] edges;

};

struct bsp_Edge
{

    public Vector3 v1;
    public Vector3 v2;

    public int v1_index;
    public int v2_index;

}

struct bsp_TexInfo
{

    public Vector3 vec3s;
    public float offs;
    public Vector3 vec3t;
    public float offt;
    public UInt32 miptex;
    public UInt32 flags; // 0x1 = sky, 0x2 = slime, 0x4 = water, 0x8 = lava

}

struct bsp_MipTexHeader
{

    public long numtex;
    public long[] offsets;

}

struct bsp_MipTex
{

    public string name;
    public UInt32 width;
    public UInt32 height;
    public UInt32 offsets1;
    public UInt32 offsets2;
    public UInt32 offsets4;
    public UInt32 offsets8;

    public Material material;

}

struct bsp_Color
{

    public byte r;
    public byte g;
    public byte b;

}

public class MapLoader : MonoBehaviour
{

    string[] Lumps = { "ENTITIES", "PLANES", "TEXTURES", "VERTICES", "VISIBILITY", "NODES", "TEXINFO", "FACES", "LIGHTING", "CLIPNODES", "LEAVES", "MARKSURFACES", "EDGES", "SURFEDGES", "MODELS", "HEADER_LUMPS" };
    Vector3[] Vertices;
    bsp_Edge[] Edges;
    bsp_Edge[] FaceEdges;
    int[] FaceEdgesIndices;
    bsp_Face[] Faces;
    bsp_TexInfo[] TexInfo;
    bsp_MipTex[] MipTex;

    // Start is called before the first frame update
    void Start()
    {

        FileStream fs = new FileStream("Assets/e1m1.bsp", FileMode.Open, FileAccess.Read);

        byte[] fileBytes = System.IO.File.ReadAllBytes("Assets/e1m1.bsp");

        bsp_Header header = new bsp_Header
        {
            magic = BitConverter.ToUInt32(fileBytes, 0),
            lumps = new Dictionary<string, bsp_Lump>()
        };

        Debug.Log(header.magic);

        for (int i = 0; i < 15; i++)
        {

            bsp_Lump lump = new bsp_Lump
            {
                offset = BitConverter.ToUInt32(fileBytes, 4 + (i * 8)),
                length = BitConverter.ToUInt32(fileBytes, 8 + (i * 8)),
                name = Lumps[i]
            };

            header.lumps.Add(lump.name, lump);
            //Debug.Log(lump.name + ": " + lump.offset + "-" + lump.length);

        }

        parseEntities(fileBytes, header.lumps["ENTITIES"]);
        parseVertices(fileBytes, header.lumps["VERTICES"]);
        parseEdges(fileBytes, header.lumps["EDGES"]);
        parseFaceEdges(fileBytes, header.lumps["SURFEDGES"]);
        parseFaces(fileBytes, header.lumps["FACES"]);
        parseTexInfo(fileBytes, header.lumps["TEXINFO"]);
        parseMipTex(fileBytes, header.lumps["TEXTURES"]);

        renderFaces();

        //gameObject.transform.Rotate(-90, 0, 0);

    }

    void parseEntities(byte[] fileBytes, bsp_Lump lump)
    {

        string entities = System.Text.Encoding.UTF8.GetString(fileBytes, (int)lump.offset, (int)lump.length);

        Debug.Log(entities);

    }

    void parseVertices(byte[] fileBytes, bsp_Lump lump)
    {

        Vertices = new Vector3[lump.length / 12];

        for (int i = 0; i < lump.length; i += 12)
        {

            float x = -(BitConverter.ToSingle(fileBytes, (int)lump.offset + i) * 0.1f);
            float y = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 4) * 0.1f;
            float z = (BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 8) * 0.1f);

            //Debug.Log(x + ", " + y + ", " + z);

            this.Vertices[i / 12] = new Vector3(y, z, x);
            //this.Vertices[i / 12] = new Vector3((float)(x * 0.1) ,(float)(y * 0.1), (float)(z * 0.1));

        }

    }

    void parseEdges(byte[] fileBytes, bsp_Lump lump)
    {

        Edges = new bsp_Edge[lump.length / 4];

        for (int i = 0; i < lump.length; i += 4)
        {

            UInt16 v1 = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i);
            UInt16 v2 = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i + 2);

            Vector3 v1_pos = Vertices[v1];
            Vector3 v2_pos = Vertices[v2];

            bsp_Edge edge = new bsp_Edge
            {
                v1 = v1_pos,
                v2 = v2_pos,

                v1_index = v1,
                v2_index = v2
            };

            //Debug.DrawLine(v1_pos, v2_pos, randomColor(), 1000f);

            Edges[i / 4] = edge;

        }

    }

    void parseFaceEdges(byte[] fileBytes, bsp_Lump lump)
    {

        // The face edge lump is simply an array of unsigned 32-bit integers.
        FaceEdges = new bsp_Edge[lump.length / 4];
        FaceEdgesIndices = new int[lump.length / 4];

        for (int i = 0; i < lump.length; i += 4)
        {

            Int32 edge = BitConverter.ToInt32(fileBytes, (int)lump.offset + i);
            FaceEdgesIndices[i / 4] = edge;

        }

    }

    void parseFaces(byte[] fileBytes, bsp_Lump lump)
    {

        Faces = new bsp_Face[lump.length / 20];

        for (int i = 0; i < lump.length; i += 20)
        {

            UInt16 plane = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i);
            UInt16 plane_side = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i + 2);
            UInt32 first_edge = BitConverter.ToUInt32(fileBytes, (int)lump.offset + i + 4);
            UInt16 num_edges = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i + 8);
            UInt16 tex_info = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i + 10);
            UInt16 styles = BitConverter.ToUInt16(fileBytes, (int)lump.offset + i + 12);
            UInt32 light_offset = BitConverter.ToUInt32(fileBytes, (int)lump.offset + i + 16);

            bsp_Edge[] edges = new bsp_Edge[num_edges];

            for (int j = 0; j < num_edges; j++)
            {
                bsp_Edge e = FaceEdges[first_edge + j];
                edges[j] = e;
            }

            bsp_Face face = new bsp_Face
            {
                plane = plane,
                plane_side = plane_side,
                first_edge = first_edge,
                num_edges = num_edges,
                tex_info = tex_info,
                styles = styles,
                light_offset = light_offset,
                edges = edges
            };

            Faces[i / 20] = face;

        }

    }

    void parseTexInfo(byte[] fileBytes, bsp_Lump lump)
    {

        TexInfo = new bsp_TexInfo[lump.length / 40];

        for (int i = 0; i < lump.length; i += 40)
        {

            Single sx = BitConverter.ToSingle(fileBytes, (int)lump.offset + i) * 0.1f;
            Single sy = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 4) * 0.1f;
            Single sz = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 8) * 0.1f;

            Vector3 vec3s = new Vector3(sy, sz, -sx);
            float offs = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 12) * 0.1f;
            
            Single tx = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 16) * 0.1f;
            Single ty = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 20) * 0.1f;
            Single tz = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 24) * 0.1f;
            
            Vector3 vec3t = new Vector3(ty, tz, -tx);
            float offt = BitConverter.ToSingle(fileBytes, (int)lump.offset + i + 28) * 0.1f;

            UInt32 miptex = BitConverter.ToUInt32(fileBytes, (int)lump.offset + i + 32);
            UInt32 flags = BitConverter.ToUInt32(fileBytes, (int)lump.offset + i + 36);

            bsp_TexInfo texinfo = new bsp_TexInfo
            {
                vec3s = vec3s,
                offs = offs,
                vec3t = vec3t,
                offt = offt,
                miptex = miptex,
                flags = flags
            };

            TexInfo[i / 40] = texinfo;

        }

    }

    void parseMipTex(byte[] fileBytes, bsp_Lump lump)
    {

        long textureCount = BitConverter.ToInt32(fileBytes, (int)lump.offset);
        long[] textureOffsets = new long[textureCount];

        MipTex = new bsp_MipTex[textureCount];

        Debug.Log("numtex: " + textureCount);

        for (int i = 0; i < textureCount; i++)
        {

            Int32 offset = BitConverter.ToInt32(fileBytes, (int)lump.offset + 4 + (i * 4));
            textureOffsets[i] = offset;

            MipTex[i] = parseMipLump(fileBytes, (int)lump.offset + offset, i);

        }

    }

    byte[] palette = {
    0x00, 0x00, 0x00, 0x0f, 0x0f, 0x0f, 0x1f, 0x1f, 0x1f, 0x2f, 0x2f, 0x2f,
    0x3f, 0x3f, 0x3f, 0x4b, 0x4b, 0x4b, 0x5b, 0x5b, 0x5b, 0x6b, 0x6b, 0x6b,
    0x7b, 0x7b, 0x7b, 0x8b, 0x8b, 0x8b, 0x9b, 0x9b, 0x9b, 0xab, 0xab, 0xab,
    0xbb, 0xbb, 0xbb, 0xcb, 0xcb, 0xcb, 0xdb, 0xdb, 0xdb, 0xeb, 0xeb, 0xeb,
    0x0f, 0x0b, 0x07, 0x17, 0x0f, 0x0b, 0x1f, 0x17, 0x0b, 0x27, 0x1b, 0x0f,
    0x2f, 0x23, 0x13, 0x37, 0x2b, 0x17, 0x3f, 0x2f, 0x17, 0x4b, 0x37, 0x1b,
    0x53, 0x3b, 0x1b, 0x5b, 0x43, 0x1f, 0x63, 0x4b, 0x1f, 0x6b, 0x53, 0x1f,
    0x73, 0x57, 0x1f, 0x7b, 0x5f, 0x23, 0x83, 0x67, 0x23, 0x8f, 0x6f, 0x23,
    0x0b, 0x0b, 0x0f, 0x13, 0x13, 0x1b, 0x1b, 0x1b, 0x27, 0x27, 0x27, 0x33,
    0x2f, 0x2f, 0x3f, 0x37, 0x37, 0x4b, 0x3f, 0x3f, 0x57, 0x47, 0x47, 0x67,
    0x4f, 0x4f, 0x73, 0x5b, 0x5b, 0x7f, 0x63, 0x63, 0x8b, 0x6b, 0x6b, 0x97,
    0x73, 0x73, 0xa3, 0x7b, 0x7b, 0xaf, 0x83, 0x83, 0xbb, 0x8b, 0x8b, 0xcb,
    0x00, 0x00, 0x00, 0x07, 0x07, 0x00, 0x0b, 0x0b, 0x00, 0x13, 0x13, 0x00,
    0x1b, 0x1b, 0x00, 0x23, 0x23, 0x00, 0x2b, 0x2b, 0x07, 0x2f, 0x2f, 0x07,
    0x37, 0x37, 0x07, 0x3f, 0x3f, 0x07, 0x47, 0x47, 0x07, 0x4b, 0x4b, 0x0b,
    0x53, 0x53, 0x0b, 0x5b, 0x5b, 0x0b, 0x63, 0x63, 0x0b, 0x6b, 0x6b, 0x0f,
    0x07, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x17, 0x00, 0x00, 0x1f, 0x00, 0x00,
    0x27, 0x00, 0x00, 0x2f, 0x00, 0x00, 0x37, 0x00, 0x00, 0x3f, 0x00, 0x00,
    0x47, 0x00, 0x00, 0x4f, 0x00, 0x00, 0x57, 0x00, 0x00, 0x5f, 0x00, 0x00,
    0x67, 0x00, 0x00, 0x6f, 0x00, 0x00, 0x77, 0x00, 0x00, 0x7f, 0x00, 0x00,
    0x13, 0x13, 0x00, 0x1b, 0x1b, 0x00, 0x23, 0x23, 0x00, 0x2f, 0x2b, 0x00,
    0x37, 0x2f, 0x00, 0x43, 0x37, 0x00, 0x4b, 0x3b, 0x07, 0x57, 0x43, 0x07,
    0x5f, 0x47, 0x07, 0x6b, 0x4b, 0x0b, 0x77, 0x53, 0x0f, 0x83, 0x57, 0x13,
    0x8b, 0x5b, 0x13, 0x97, 0x5f, 0x1b, 0xa3, 0x63, 0x1f, 0xaf, 0x67, 0x23,
    0x23, 0x13, 0x07, 0x2f, 0x17, 0x0b, 0x3b, 0x1f, 0x0f, 0x4b, 0x23, 0x13,
    0x57, 0x2b, 0x17, 0x63, 0x2f, 0x1f, 0x73, 0x37, 0x23, 0x7f, 0x3b, 0x2b,
    0x8f, 0x43, 0x33, 0x9f, 0x4f, 0x33, 0xaf, 0x63, 0x2f, 0xbf, 0x77, 0x2f,
    0xcf, 0x8f, 0x2b, 0xdf, 0xab, 0x27, 0xef, 0xcb, 0x1f, 0xff, 0xf3, 0x1b,
    0x0b, 0x07, 0x00, 0x1b, 0x13, 0x00, 0x2b, 0x23, 0x0f, 0x37, 0x2b, 0x13,
    0x47, 0x33, 0x1b, 0x53, 0x37, 0x23, 0x63, 0x3f, 0x2b, 0x6f, 0x47, 0x33,
    0x7f, 0x53, 0x3f, 0x8b, 0x5f, 0x47, 0x9b, 0x6b, 0x53, 0xa7, 0x7b, 0x5f,
    0xb7, 0x87, 0x6b, 0xc3, 0x93, 0x7b, 0xd3, 0xa3, 0x8b, 0xe3, 0xb3, 0x97,
    0xab, 0x8b, 0xa3, 0x9f, 0x7f, 0x97, 0x93, 0x73, 0x87, 0x8b, 0x67, 0x7b,
    0x7f, 0x5b, 0x6f, 0x77, 0x53, 0x63, 0x6b, 0x4b, 0x57, 0x5f, 0x3f, 0x4b,
    0x57, 0x37, 0x43, 0x4b, 0x2f, 0x37, 0x43, 0x27, 0x2f, 0x37, 0x1f, 0x23,
    0x2b, 0x17, 0x1b, 0x23, 0x13, 0x13, 0x17, 0x0b, 0x0b, 0x0f, 0x07, 0x07,
    0xbb, 0x73, 0x9f, 0xaf, 0x6b, 0x8f, 0xa3, 0x5f, 0x83, 0x97, 0x57, 0x77,
    0x8b, 0x4f, 0x6b, 0x7f, 0x4b, 0x5f, 0x73, 0x43, 0x53, 0x6b, 0x3b, 0x4b,
    0x5f, 0x33, 0x3f, 0x53, 0x2b, 0x37, 0x47, 0x23, 0x2b, 0x3b, 0x1f, 0x23,
    0x2f, 0x17, 0x1b, 0x23, 0x13, 0x13, 0x17, 0x0b, 0x0b, 0x0f, 0x07, 0x07,
    0xdb, 0xc3, 0xbb, 0xcb, 0xb3, 0xa7, 0xbf, 0xa3, 0x9b, 0xaf, 0x97, 0x8b,
    0xa3, 0x87, 0x7b, 0x97, 0x7b, 0x6f, 0x87, 0x6f, 0x5f, 0x7b, 0x63, 0x53,
    0x6b, 0x57, 0x47, 0x5f, 0x4b, 0x3b, 0x53, 0x3f, 0x33, 0x43, 0x33, 0x27,
    0x37, 0x2b, 0x1f, 0x27, 0x1f, 0x17, 0x1b, 0x13, 0x0f, 0x0f, 0x0b, 0x07,
    0x6f, 0x83, 0x7b, 0x67, 0x7b, 0x6f, 0x5f, 0x73, 0x67, 0x57, 0x6b, 0x5f,
    0x4f, 0x63, 0x57, 0x47, 0x5b, 0x4f, 0x3f, 0x53, 0x47, 0x37, 0x4b, 0x3f,
    0x2f, 0x43, 0x37, 0x2b, 0x3b, 0x2f, 0x23, 0x33, 0x27, 0x1f, 0x2b, 0x1f,
    0x17, 0x23, 0x17, 0x0f, 0x1b, 0x13, 0x0b, 0x13, 0x0b, 0x07, 0x0b, 0x07,
    0xff, 0xf3, 0x1b, 0xef, 0xdf, 0x17, 0xdb, 0xcb, 0x13, 0xcb, 0xb7, 0x0f,
    0xbb, 0xa7, 0x0f, 0xab, 0x97, 0x0b, 0x9b, 0x83, 0x07, 0x8b, 0x73, 0x07,
    0x7b, 0x63, 0x07, 0x6b, 0x53, 0x00, 0x5b, 0x47, 0x00, 0x4b, 0x37, 0x00,
    0x3b, 0x2b, 0x00, 0x2b, 0x1f, 0x00, 0x1b, 0x0f, 0x00, 0x0b, 0x07, 0x00,
    0x00, 0x00, 0xff, 0x0b, 0x0b, 0xef, 0x13, 0x13, 0xdf, 0x1b, 0x1b, 0xcf,
    0x23, 0x23, 0xbf, 0x2b, 0x2b, 0xaf, 0x2f, 0x2f, 0x9f, 0x2f, 0x2f, 0x8f,
    0x2f, 0x2f, 0x7f, 0x2f, 0x2f, 0x6f, 0x2f, 0x2f, 0x5f, 0x2b, 0x2b, 0x4f,
    0x23, 0x23, 0x3f, 0x1b, 0x1b, 0x2f, 0x13, 0x13, 0x1f, 0x0b, 0x0b, 0x0f,
    0x2b, 0x00, 0x00, 0x3b, 0x00, 0x00, 0x4b, 0x07, 0x00, 0x5f, 0x07, 0x00,
    0x6f, 0x0f, 0x00, 0x7f, 0x17, 0x07, 0x93, 0x1f, 0x07, 0xa3, 0x27, 0x0b,
    0xb7, 0x33, 0x0f, 0xc3, 0x4b, 0x1b, 0xcf, 0x63, 0x2b, 0xdb, 0x7f, 0x3b,
    0xe3, 0x97, 0x4f, 0xe7, 0xab, 0x5f, 0xef, 0xbf, 0x77, 0xf7, 0xd3, 0x8b,
    0xa7, 0x7b, 0x3b, 0xb7, 0x9b, 0x37, 0xc7, 0xc3, 0x37, 0xe7, 0xe3, 0x57,
    0x7f, 0xbf, 0xff, 0xab, 0xe7, 0xff, 0xd7, 0xff, 0xff, 0x67, 0x00, 0x00,
    0x8b, 0x00, 0x00, 0xb3, 0x00, 0x00, 0xd7, 0x00, 0x00, 0xff, 0x00, 0x00,
    0xff, 0xf3, 0x93, 0xff, 0xf7, 0xc7, 0xff, 0xff, 0xff, 0x9f, 0x5b, 0x53,
};

    bsp_MipTex parseMipLump(byte[] fileBytes, Int32 offset, int index)
    {

        string name = System.Text.Encoding.UTF8.GetString(fileBytes, offset, 16);
        UInt32 width = BitConverter.ToUInt32(fileBytes, offset + 16);
        UInt32 height = BitConverter.ToUInt32(fileBytes, offset + 20);
        UInt32 offsets1 = BitConverter.ToUInt32(fileBytes, offset + 24);
        // we dont actualy need all the scaled versions of the textures because of computer graphics being better then 1997
        UInt32 offsets2 = BitConverter.ToUInt32(fileBytes, offset + 28);
        UInt32 offsets4 = BitConverter.ToUInt32(fileBytes, offset + 32);
        UInt32 offsets8 = BitConverter.ToUInt32(fileBytes, offset + 36);

        bsp_MipTex miptex = new bsp_MipTex
        {
            name = name,
            width = width,
            height = height,
            offsets1 = offsets1,
            offsets2 = offsets2,
            offsets4 = offsets4,
            offsets8 = offsets8
        };

        UInt32 palletteOffset = (uint)((UInt32)offset + offsets8 + Math.Floor((double)(width * height / 64))) + 2;
        bsp_Color[] colors = new bsp_Color[256];
        for (int i = 0; i < 256; i++)
        {

            byte r = palette[i * 3];
            byte g = palette[i * 3 + 1];
            byte b = palette[i * 3 + 2];

            colors[i] = new bsp_Color
            {
                r = r,
                g = g,
                b = b
            };

        }

        // read the texture data
        Texture2D texture = new Texture2D((int)width, (int)height);

        for (int i = 0; i < width * height; i++)
        {

            byte r = colors[fileBytes[offset + offsets1 + i]].r;
            byte g = colors[fileBytes[offset + offsets1 + i]].g;
            byte b = colors[fileBytes[offset + offsets1 + i]].b;

            texture.SetPixel(i % (int)width, i / (int)width, new Color32(r, g, b, 1));

        }

        texture.Apply();

        // create a material and a new gameobject to hold it
        Material material = new Material(Shader.Find("Standard"));
        material.mainTexture = texture;

        miptex.material = material;

        // create a plane to put the texture on
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.transform.parent = gameObject.transform;
        plane.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        plane.transform.localPosition = new Vector3((float)1.3 * (float)index, 30, 0);
        plane.GetComponent<MeshRenderer>().material = material;

        return miptex;

    }

    void renderFaces()
    {
        foreach (bsp_Face face in Faces)
        {
            GameObject thisFaceMesh = GenerateFaceObject(face);
        }
    }

    GameObject GenerateFaceObject(bsp_Face face)
    {
        GameObject faceObject = new GameObject("BSPface");
        faceObject.transform.parent = gameObject.transform;
        Mesh faceMesh = new Mesh();
        faceMesh.name = "BSPmesh";

        // grab our verts
        Vector3[] verts = new Vector3[face.num_edges];
        uint edgestep = face.first_edge;
        for (int i = 0; i < face.num_edges; i++)
        {
            if (FaceEdgesIndices[face.first_edge + i] < 0)
            {
                verts[i] = Vertices[Edges[Mathf.Abs(FaceEdgesIndices[edgestep])].v1_index];
            }
            else
            {
                verts[i] = Vertices[Edges[FaceEdgesIndices[edgestep]].v2_index];
            }
            edgestep++;
        }

        // whip up tris
        int[] tris = new int[(face.num_edges - 2) * 3];
        int tristep = 1;
        for (int i = 1; i < verts.Length - 1; i++)
        {
            tris[tristep - 1] = 0;
            tris[tristep] = i;
            tris[tristep + 1] = i + 1;
            tristep += 3;
        }

        // whip up uvs
        Vector2[] uvs = new Vector2[verts.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            float s = Vector3.Dot(verts[i], TexInfo[face.tex_info].vec3s) + TexInfo[face.tex_info].offs;
            float t = Vector3.Dot(verts[i], TexInfo[face.tex_info].vec3t) + TexInfo[face.tex_info].offt;

            uvs[i] = new Vector2(s, t);
        }

        //Array.Reverse(tris);

        faceMesh.vertices = verts;
        faceMesh.triangles = tris;
        faceMesh.uv = uvs;
        faceMesh.RecalculateNormals();
        faceObject.AddComponent<MeshFilter>();
        faceObject.GetComponent<MeshFilter>().mesh = faceMesh;
        faceObject.AddComponent<MeshRenderer>();
        faceObject.AddComponent<MeshCollider>();

        //faceObject.isStatic = true;

        // get face miptex
        faceObject.GetComponent<MeshRenderer>().material = MipTex[TexInfo[face.tex_info].miptex].material;

        return faceObject;
    }

    Color randomColor()
    {

        return new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);

    }

    // Update is called once per frame
    void Update()
    {

    }
}
