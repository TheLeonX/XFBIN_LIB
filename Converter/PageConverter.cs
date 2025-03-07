using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using XFBIN_LIB;
using XFBIN_LIB.XFBIN;

namespace XFBIN_LIB.Converter {
    public class ReadPageConverter : JsonConverter<READ_PAGE> {
        public override READ_PAGE Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token");


            READ_PAGE new_page = new READ_PAGE();
            READ_CHUNK_MAP chunk_map = new READ_CHUNK_MAP();
            READ_EXTRA_CHUNK_MAP_INDICES extra_map = new READ_EXTRA_CHUNK_MAP_INDICES();
            READ_CHUNK chunk = new READ_CHUNK();
            while (reader.Read()) {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new_page;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected PropertyName token");

                var propName = reader.GetString();
                reader.Read();

                switch (propName) {
                    case "Chunk Maps":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                            if (reader.TokenType == JsonTokenType.PropertyName) {
                                //Console.WriteLine(reader.GetString());
                                switch (reader.GetString()) {
                                    case "Name":
                                        chunk_map = new READ_CHUNK_MAP();
                                        reader.Read();
                                        chunk_map.ChunkName = reader.GetString();
                                        break;
                                    case "Type":
                                        reader.Read();
                                        chunk_map.TypeName = reader.GetString();
                                        break;
                                    case "Path":
                                        reader.Read();
                                        chunk_map.FilePathName = reader.GetString();
                                        new_page.ChunkMappings.Add(chunk_map);
                                        break;
                                }
                            }
                        }
                        break;
                    case "Chunk References":
                        int offset = 0;

                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {



                            if (reader.TokenType == JsonTokenType.String) {
                                switch (offset) {
                                    case 0:
                                        extra_map = new READ_EXTRA_CHUNK_MAP_INDICES();
                                        extra_map.ExtraChunkMapName = reader.GetString();
                                        offset++;
                                        break;
                                    case 1:
                                        extra_map.ChunkMapName = reader.GetString();
                                        offset++;
                                        break;
                                    case 2:
                                        extra_map.ChunkTypeName = reader.GetString();
                                        offset++;
                                        break;
                                    case 3:
                                        extra_map.ChunkPathName = reader.GetString();
                                        new_page.ExtraMappings.Add(extra_map);
                                        offset = 0;
                                        break;
                                }
                            }

                        }
                        break;
                    case "Chunks":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                            if (reader.TokenType == JsonTokenType.PropertyName) {
                                switch (reader.GetString()) {
                                    case "File Name":
                                        chunk = new READ_CHUNK();
                                        reader.Read();
                                        chunk.file_name = reader.GetString();
                                        break;
                                    case "Version":
                                        reader.Read();
                                        chunk.Version = reader.GetUInt16();
                                        break;
                                    case "Version Attribute":
                                        reader.Read();
                                        chunk.VersionAttribute = reader.GetUInt16();
                                        break;
                                    case "Chunk":
                                        reader.Read();
                                        break;
                                    case "Name":
                                        reader.Read();
                                        chunk.ChunkMapName = reader.GetString();
                                        break;
                                    case "Type":
                                        reader.Read();
                                        chunk.ChunkTypeName = reader.GetString();
                                        break;
                                    case "Path":
                                        reader.Read();
                                        chunk.ChunkPathName = reader.GetString();
                                        new_page.Chunks.Add(chunk);
                                        break;
                                }
                            }

                        }
                        break;
                }
            }

            throw new JsonException("Expected EndObject token");
        }
        public override void Write(Utf8JsonWriter writer, READ_PAGE value, JsonSerializerOptions options) {

        }
     }

    public class PageConverter : JsonConverter<PAGE> {
        public static Dictionary<string, string> file_format = new Dictionary<string, string>(){
            { "nuccChunkUnknown", ".unk"},
            { "nuccChunkClump", ".clump"},
            { "nuccChunkAnm", ".anm"},
            { "nuccChunkTexture", ".nut"},
            { "nuccChunkAnmStrm", ".anmstrm"},
            { "nuccChunkAnmStrmFrame", ".anmstrmframe"},
            { "nuccChunkModel", ".model"},
            { "nuccChunkModelHit", ".modelhit"},
            { "nuccChunkMaterial", ".material"},
            { "nuccChunkNub", ".nub"},
            { "nuccChunkCoord", ".coord"},
            { "nuccChunkDynamics", ".dynamics"},
            { "nuccChunkTrail", ".trail"},
            { "nuccChunkBillboard", ".billboard"},
            { "nuccChunkBinary", ".bin"},
            { "nuccChunkParticle", ".particle"},
            { "nuccChunkPrimitiveVertex", ".primver"},
            { "nuccChunkModelPrimitiveBatch", ".modprimbatch"},
            { "nuccChunkCamera", ".camera"},
            { "nuccChunkSprite", ".sprite"},
            { "nuccChunkLightDirc", ".light_dirc"},
            { "nuccChunkLightPoint", ".light_point"},
            { "nuccChunkAmbient", ".ambient"},
            { "nuccChunkSpriteAnm", ".spriteanm"},
            { "nuccChunkFont", ".font"},
            { "nuccChunkMorphPrimitive", ".morphprim"},
            { "nuccChunkLayerSet", ".layerset"},
            { "nuccChunkModelVertex", ".modelvert"},
            { "nuccChunkLightSet", ".light_set"},
            { "nuccChunkSprite2", ".sprite2"},
            { "nuccChunkSprite2Anm", ".sprite2anm"},
        };
        public override PAGE Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            return null;
        }
        public override void Write(Utf8JsonWriter writer, PAGE value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // "Chunk Maps" section
            writer.WriteStartArray("Chunk Maps");
            foreach (CHUNK_MAP chunk_map in value.ChunkMappings)
            {
                writer.WriteStartObject();
                int nameIdx = (int)chunk_map.ChunkNameIndex;
                string name = (nameIdx >= 0 && nameIdx < value.ChunkTable.ChunkNames.Count)
                                ? value.ChunkTable.ChunkNames[nameIdx].ChunkName
                                : "";
                writer.WriteString("Name", name);

                int typeIdx = (int)chunk_map.ChunkTypeIndex;
                string typeName = (typeIdx >= 0 && typeIdx < value.ChunkTable.ChunkTypes.Count)
                                  ? value.ChunkTable.ChunkTypes[typeIdx].ChunkTypeName
                                  : "";
                writer.WriteString("Type", typeName);

                int pathIdx = (int)chunk_map.FilePathIndex;
                string pathName = (pathIdx >= 0 && pathIdx < value.ChunkTable.FilePaths.Count)
                                  ? value.ChunkTable.FilePaths[pathIdx].FilePathName
                                  : "";
                writer.WriteString("Path", pathName);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            // "Chunk References" section
            writer.WriteStartArray("Chunk References");
            foreach (EXTRA_CHUNK_MAP_INDICES extra_map in value.ExtraMappings)
            {
                writer.WriteStartObject();
                int extraNameIdx = (int)extra_map.ChunkNameIndex;
                string extraName = (extraNameIdx >= 0 && extraNameIdx < value.ChunkTable.ChunkNames.Count)
                                   ? value.ChunkTable.ChunkNames[extraNameIdx].ChunkName
                                   : "";
                writer.WriteString("Name", extraName);

                writer.WriteStartObject("Chunk");
                int extraMapIdx = (int)extra_map.ChunkMapIndex;
                if (extraMapIdx >= 0 && extraMapIdx < value.ChunkTable.ChunkMaps.Count)
                {
                    CHUNK_MAP map = value.ChunkTable.ChunkMaps[extraMapIdx];

                    int mapNameIdx = (int)map.ChunkNameIndex;
                    string mapName = (mapNameIdx >= 0 && mapNameIdx < value.ChunkTable.ChunkNames.Count)
                                     ? value.ChunkTable.ChunkNames[mapNameIdx].ChunkName
                                     : "";
                    writer.WriteString("Name", mapName);

                    int mapTypeIdx = (int)map.ChunkTypeIndex;
                    string mapType = (mapTypeIdx >= 0 && mapTypeIdx < value.ChunkTable.ChunkTypes.Count)
                                     ? value.ChunkTable.ChunkTypes[mapTypeIdx].ChunkTypeName
                                     : "";
                    writer.WriteString("Type", mapType);

                    int mapPathIdx = (int)map.FilePathIndex;
                    string mapPath = (mapPathIdx >= 0 && mapPathIdx < value.ChunkTable.FilePaths.Count)
                                     ? value.ChunkTable.FilePaths[mapPathIdx].FilePathName
                                     : "";
                    writer.WriteString("Path", mapPath);
                } else
                {
                    writer.WriteNull("Name");
                    writer.WriteNull("Type");
                    writer.WriteNull("Path");
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            // "Chunks" section
            writer.WriteStartArray("Chunks");
            foreach (CHUNK chunk in value.Chunks)
            {
                int chunkMapIndexIdx = (int)chunk.ChunkMapIndex;
                if (chunkMapIndexIdx < 0 || chunkMapIndexIdx >= value.ChunkTable.ChunkMapIndices.Count)
                    continue;

                int actualChunkMapIndex = (int)value.ChunkTable.ChunkMapIndices[chunkMapIndexIdx].ChunkMapIndex;
                if (actualChunkMapIndex < 0 || actualChunkMapIndex >= value.ChunkTable.ChunkMaps.Count)
                    continue;

                CHUNK_MAP chunkMap = value.ChunkTable.ChunkMaps[actualChunkMapIndex];

                int filePathIdx = (int)chunkMap.FilePathIndex;
                if (filePathIdx < 0 || filePathIdx >= value.ChunkTable.FilePaths.Count)
                    continue;
                string filePathName = value.ChunkTable.FilePaths[filePathIdx].FilePathName;
                if (string.IsNullOrEmpty(filePathName))
                    continue;

                int chunkTypeIdx = (int)chunkMap.ChunkTypeIndex;
                string type = (chunkTypeIdx >= 0 && chunkTypeIdx < value.ChunkTable.ChunkTypes.Count)
                              ? value.ChunkTable.ChunkTypes[chunkTypeIdx].ChunkTypeName
                              : "";
                string format = ".bin";
                if (PageConverter.file_format.ContainsKey(type))
                    format = PageConverter.file_format[type];

                if (type == "nuccChunkNull" || type == "nuccChunkPage" || type == "nuccChunkIndex")
                    continue;

                int chunkNameIdx = (int)chunkMap.ChunkNameIndex;
                string chunkName = (chunkNameIdx >= 0 && chunkNameIdx < value.ChunkTable.ChunkNames.Count)
                                   ? value.ChunkTable.ChunkNames[chunkNameIdx].ChunkName
                                   : "";

                writer.WriteStartObject();
                writer.WriteString("File Name", chunkName + format);
                writer.WriteNumber("Version", chunk.Version);
                writer.WriteNumber("Version Attribute", chunk.VersionAttribute);
                writer.WriteStartObject("Chunk");
                writer.WriteString("Name", chunkName);
                writer.WriteString("Type", type);
                writer.WriteString("Path", filePathName);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

    }
}
