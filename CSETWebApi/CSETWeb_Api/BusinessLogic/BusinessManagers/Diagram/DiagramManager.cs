﻿//////////////////////////////// 
// 
//   Copyright 2019 Battelle Energy Alliance, LLC  
// 
// 
////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.IO;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.EntityFrameworkCore;
using DataLayerCore.Model;
using CSETWeb_Api.BusinessLogic.BusinessManagers.Diagram;
using CSETWeb_Api.BusinessLogic.Models;
using CSETWeb_Api.Models;
using CSETWeb_Api.BusinessLogic.BusinessManagers.Diagram.layers;

namespace CSETWeb_Api.BusinessManagers
{
    public class DiagramManager
    {
        private CSET_Context db;

        public DiagramManager(CSET_Context db)
        {
            this.db = db;
        }

        /// <summary>
        /// Persists the diagram XML in the database.
        /// </summary>
        /// <param name="assessmentID"></param>
        /// <param name="diagramXML"></param>
        public void SaveDiagram(int assessmentID, XmlDocument xDoc, int lastUsedComponentNumber, string diagramImage)
        {
            // the front end sometimes calls 'save' with an empty graph on open.  
            // Need to prevent the javascript from doing that on open, but for now,
            // let's detect an empty graph and not save it.

            var cellCount = xDoc.SelectNodes("//root/mxCell").Count;
            var objectCount = xDoc.SelectNodes("//root/object").Count;
            if (cellCount == 2 && objectCount == 0)
            {
                // Update 29-Aug-2019 RKW - we are no longer getting the save calls on open.
                // Allow all save calls with an empty graph.
                // return;
            }

            using (var db = new CSET_Context())
            {
                var assessmentRecord = db.ASSESSMENTS.Where(x => x.Assessment_Id == assessmentID).FirstOrDefault();
                if (assessmentRecord != null)
                {
                    DiagramDifferenceManager differenceManager = new DiagramDifferenceManager(db);
                    XmlDocument oldDoc = new XmlDocument();
                    if (!String.IsNullOrWhiteSpace(assessmentRecord.Diagram_Markup))
                    {
                        oldDoc.LoadXml(assessmentRecord.Diagram_Markup);
                    }
                    differenceManager.buildDiagramDictionaries(xDoc, oldDoc);
                    differenceManager.SaveDifferences(assessmentID);
                }
                else
                {
                    //what the?? where is our assessment
                    throw new ApplicationException("Assessment record is missing for id" + assessmentID);
                }

                assessmentRecord.LastUsedComponentNumber = lastUsedComponentNumber;
                String diagramXML = xDoc.OuterXml;
                if (!String.IsNullOrWhiteSpace(diagramXML))
                {
                    assessmentRecord.Diagram_Markup = diagramXML;
                }
                assessmentRecord.Diagram_Image = diagramImage;

                db.SaveChanges();
            }
            
         
        }


        /// <summary>
        /// Returns the diagram XML for the assessment ID.  
        /// </summary>
        /// <param name="assessmentID"></param>
        /// <returns></returns>
        public DiagramResponse GetDiagram(int assessmentID)
        {
           
                var assessmentRecord = db.ASSESSMENTS.Where(x => x.Assessment_Id == assessmentID).FirstOrDefault();

                DiagramResponse resp = new DiagramResponse();

                if (assessmentRecord != null)
                {
                    resp.DiagramXml = assessmentRecord.Diagram_Markup;
                    resp.LastUsedComponentNumber = assessmentRecord.LastUsedComponentNumber;
                    return resp;
                }

                return null;           
        }


        /// <summary>
        /// Returns a boolean indicating the presence of a diagram.
        /// </summary>
        /// <param name="assessmentID"></param>
        /// <returns></returns>
        public bool HasDiagram(int assessmentID)
        {
            using (var db = new CSET_Context())
            {
                var assessmentRecord = db.ASSESSMENTS.Where(x => x.Assessment_Id == assessmentID).FirstOrDefault();

                DiagramResponse resp = new DiagramResponse();

                if (assessmentRecord != null)
                {
                    return assessmentRecord.Diagram_Markup != null;
                }

                return false;
            }
        }


        /// <summary>
        /// Returns the diagram image stored in the database for the assessment.
        /// </summary>
        /// <param name="assessmentID"></param>
        /// <returns></returns>
        public string GetDiagramImage(int assessmentID)
        {
            using (var db = new CSET_Context())
            {
                var assessmentRecord = db.ASSESSMENTS.Where(x => x.Assessment_Id == assessmentID).FirstOrDefault();
                if (assessmentRecord.Diagram_Image == null)
                {
                    return string.Empty;
                }


                XmlDocument xImage = new XmlDocument();
                try
                {
                    xImage.LoadXml(assessmentRecord.Diagram_Image);
                }
                catch (Exception)
                {
                    // whatever is in the database is not XML
                    return string.Empty;
                }


                // Make sure any paths to embedded svg images are correctly qualified with this server's URL
                var serverHostUrl = System.Web.HttpContext.Current.Request.Url;
                string s = serverHostUrl.Scheme + "://" + serverHostUrl.Authority;


                var images = xImage.GetElementsByTagName("image");
                foreach (var image in images)
                {
                    var href = ((XmlElement)image).Attributes["xlink:href"];
                    if (href != null)
                    {
                        Uri u = new Uri(href.InnerText);
                        ((XmlElement)image).SetAttribute("xlink:href", s + u.LocalPath);
                    }
                }

                return xImage.OuterXml;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<ComponentSymbolGroup> GetComponentSymbols()
        {
            var resp = new List<ComponentSymbolGroup>();

            using (var db = new CSET_Context())
            {
                var symbolGroups = db.SYMBOL_GROUPS.OrderBy(x => x.Id).ToList();

                foreach (SYMBOL_GROUPS g in symbolGroups)
                {
                    var group = new ComponentSymbolGroup
                    {
                        SymbolGroupID = g.Id,
                        GroupName = g.Symbol_Group_Name,
                        SymbolGroupTitle = g.Symbol_Group_Title,
                        Symbols = new List<ComponentSymbol>()
                    };

                    resp.Add(group);

                    var symbols = db.COMPONENT_SYMBOLS.Where(x => x.Symbol_Group_Id == group.SymbolGroupID)
                        .OrderBy(x => x.Name).ToList();

                    foreach (COMPONENT_SYMBOLS s in symbols)
                    {
                        var symbol = new ComponentSymbol
                        {
                            Name = s.Name,
                            DiagramTypeXml = s.Diagram_Type_Xml,
                            Abbreviation = s.Abbreviation,
                            FileName = s.File_Name,
                            DisplayName = s.Display_Name,
                            LongName = s.Long_Name,
                            ComponentFamilyName = s.Component_Family_Name,
                            Tags = s.Tags,
                            Width = (int)s.Width,
                            Height = (int)s.Height
                        };

                        group.Symbols.Add(symbol);
                    }
                }
            }

            return resp;
        }


        /// <summary>
        /// Get all component symbols ungrouped 
        /// </summary>
        /// <returns></returns>
        public List<ComponentSymbol> GetAllComponentSymbols()
        {
            var resp = new List<ComponentSymbol>();
            using (var db = new CSET_Context())
            {


                var symbols = db.COMPONENT_SYMBOLS.OrderBy(x => x.Name).ToList();

                foreach (COMPONENT_SYMBOLS s in symbols)
                {
                    var symbol = new ComponentSymbol
                    {
                        Name = s.Name,
                        DiagramTypeXml = s.Diagram_Type_Xml,
                        Abbreviation = s.Abbreviation,
                        FileName = s.File_Name,
                        DisplayName = s.Display_Name,
                        LongName = s.Long_Name,
                        ComponentFamilyName = s.Component_Family_Name,
                        Tags = s.Tags,
                        Width = (int)s.Width,
                        Height = (int)s.Height
                    };

                    resp.Add(symbol);
                }
            }

            return resp;
        }

        /// <summary>
        /// Get xml stream for an assessment diagram
        /// </summary>
        /// <param name="assessmentId"></param>
        /// <returns></returns>
        public StringReader GetDiagramXml(int assessmentId)
        {
            var diagram = new AssessmentManager().GetAssessmentById(assessmentId)?.Diagram_Markup;
            
            if (diagram != null)
            {
               

                var stream = new StringReader(diagram);
                return stream;
            }

            return null;
        }

        /// <summary>
        /// get vertices from diagram stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public List<mxGraphModelRootObject> ProcessDiagramVertices(StringReader stream, int assessment_id)
        {

            List<mxGraphModelRootObject> vertices = new List<mxGraphModelRootObject>();
            if (stream != null)
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)deserializer.Deserialize((stream));

                mxGraphModelRootObject o = new mxGraphModelRootObject();
                Type objectType = typeof(mxGraphModelRootObject);

                LayerManager layers = new LayerManager(db, assessment_id);
                foreach (var item in diagramXml.root.Items)
                {
                    if (item.GetType() == objectType)
                    {
                       
                        var addLayerVisible = (mxGraphModelRootObject) item;
                        string parentId = !string.IsNullOrEmpty(addLayerVisible.mxCell.parent) ? addLayerVisible.mxCell.parent : addLayerVisible.parent??"0";
                        var layerVisibility = layers.GetLastLayer(parentId);
                        addLayerVisible.visible = layerVisibility.visible??"true";
                        addLayerVisible.layerName = layerVisibility.layerName??string.Empty;
                        
                        vertices.Add(addLayerVisible);
                    }

                }
            }

            return vertices;
        }

     



        /// <summary>
        /// get vertices from diagram stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public List<mxGraphModelRootMxCell> ProcessDiagramShapes(StringReader stream, int assessment_id)
        {

            List<mxGraphModelRootMxCell> vertices = new List<mxGraphModelRootMxCell>();
            if (stream != null)
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)deserializer.Deserialize((stream));

                Type objectType = typeof(mxGraphModelRootMxCell);


                foreach (var item in diagramXml.root.Items)
                {
                    if (item.GetType() == objectType)
                    {
                        var addLayerVisible = (mxGraphModelRootMxCell)item;
                        var layerVisibility = getLayerVisibility(addLayerVisible.parent, assessment_id);
                        addLayerVisible.visible = layerVisibility.visible;
                        addLayerVisible.layerName = layerVisibility.layerName;
                        vertices.Add(addLayerVisible);
                    }

                }
            }

            return vertices;
        }

        /// <summary>
        /// get edges from diagram stream
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public List<mxGraphModelRootMxCell> ProcessDigramEdges(StringReader stream, int assessment_id)
        {
            List<mxGraphModelRootMxCell> edges = new List<mxGraphModelRootMxCell>();
            if (stream != null)
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)deserializer.Deserialize((stream));

                mxGraphModelRootObject o = new mxGraphModelRootObject();
                Type objectType = typeof(mxGraphModelRootMxCell);

                foreach (var item in diagramXml.root.Items)
                {
                    if (item.GetType() == objectType)
                    {
                        var addLayerVisible = (mxGraphModelRootMxCell)item;
                        var layerVisibility = getLayerVisibility(addLayerVisible.parent, assessment_id);
                        addLayerVisible.visible = layerVisibility.visible;
                        addLayerVisible.layerName = layerVisibility.layerName;
                        edges.Add(addLayerVisible);
                    }
                }
            }

            return edges;
        }

        //TODO check to see if this is still the same
        /// <summary>
        /// 
        /// </summary>
        /// <param name="drawIoId"></param>
        /// <param name="assessment_id"></param>
        /// <returns></returns>
        public LayerVisibility getLayerVisibility(string drawIoId, int assessment_id)
        {
            //get the parent id
            var lm = new LayerManager(db, assessment_id);
            var list = lm.GetLastLayer(drawIoId);

            //Dictionary<string, LayerVisibility> allItems = list.ToDictionary(x => x.DrawIo_id, x => x);
            //LayerVisibility layer = new LayerVisibility();
            //LayerVisibility lastLayer = new LayerVisibility();
            //while (!string.IsNullOrEmpty(drawIoId) && allItems.TryGetValue(drawIoId, out layer))
            //{
            //    lastLayer = layer;
            //    drawIoId = layer.Parent_DrawIo_id;
            //}

            return list;
        }



        /// <summary>
        /// Get components from diagram xml objects
        /// </summary>
        /// <param name="diagram"></param>
        /// <returns></returns>
        public List<mxGraphModelRootObject> GetDiagramComponents(List<mxGraphModelRootObject> vertices)
        {
            try
            {
                var diagramComponents = vertices.Where(x => !string.IsNullOrEmpty(x.ComponentGuid)).ToList();
                var diagramZones = GetDiagramZones(vertices);
                var symbols = GetAllComponentSymbols();
                for (int i = 0; i < diagramComponents.Count(); i++)
                {
                    var imageTag = diagramComponents[i].mxCell.style.Split(';').FirstOrDefault(x=>x.Contains("image="));
                   
                    if (!string.IsNullOrEmpty(imageTag))
                    {
                        var image = imageTag.Split('/').LastOrDefault();
                        diagramComponents[i].assetType = symbols.FirstOrDefault(x => x.FileName == image)?.DisplayName;
                    }

                    diagramComponents[i].zoneLabel = diagramZones.FirstOrDefault(x=>x.id == diagramComponents[i].mxCell.parent)?.label;
                }

                return diagramComponents;
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            finally
            {
            }

            return null;
        }

        /// <summary>
        /// Get lines from diagram xml objects
        /// </summary>
        /// <param name="edges"></param>
        /// <returns></returns>
        public List<mxGraphModelRootMxCell> GetDiagramLinks(List<mxGraphModelRootMxCell> edges)
        {
            var diagramLines = edges.Where(l => l.edge == "1").ToList();
            return diagramLines;
        }

        /// <summary>
        /// Get zones from diagram xml objects
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public List<mxGraphModelRootObject> GetDiagramZones(List<mxGraphModelRootObject> vertices)
        {
            var diagramZones = vertices.Where(z => z.zone == "1").ToList();
            return diagramZones;
        }

        /// <summary>
        /// Get shapes from diagram xml objects
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public List<mxGraphModelRootMxCell> GetDiagramShapes(List<mxGraphModelRootMxCell> vertices)
        {
            var diagramShapes = vertices.Where(x => !string.IsNullOrEmpty(x.style) && x.style.Contains("shape=")).ToList();
            return diagramShapes;
        }

        /// <summary>
        /// Get text from diagram xml objects
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public List<mxGraphModelRootMxCell> GetDiagramText(List<mxGraphModelRootMxCell> vertices)
        {
            var diagramText = vertices.Where(x => !string.IsNullOrEmpty(x.style) && x.style.Contains("text")).ToList();
            return diagramText;
        }

        /// <summary>
        /// Update fields of link
        /// </summary>
        /// <param name="vertice"></param>
        /// <param name="assessmentId"></param>
        public void SaveLink(mxGraphModelRootMxCell vertice, int assessmentId)
        {
            try
            {
                var stream = GetDiagramXml(assessmentId);
                XmlSerializer serializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)serializer.Deserialize(stream);
                for (int i = 0; i < diagramXml.root.Items.Length; i++)
                {
                    mxGraphModelRootMxCell item = new mxGraphModelRootMxCell();
                    Type objectType = typeof(mxGraphModelRootMxCell);

                    if (diagramXml.root.Items[i].GetType() == objectType)
                    {
                        item = (mxGraphModelRootMxCell)diagramXml.root.Items[i];
                    }

                    if (item.id == vertice.id)
                    {
                        item.value = vertice.value;
                        diagramXml.root.Items[i] = (object)item;
                    }
                }

                SaveDiagramXml(assessmentId, diagramXml);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            finally
            {
            }


        }

        /// <summary>
        /// Update fields of components
        /// </summary>
        /// <param name="vertice"></param>
        /// <param name="assessmentId"></param>
        public void SaveComponent(mxGraphModelRootObject vertice, int assessmentId)
        {
            try
            {
                var stream = GetDiagramXml(assessmentId);
                XmlSerializer serializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)serializer.Deserialize(stream);
                for (int i = 0; i < diagramXml.root.Items.Length; i++)
                {
                    mxGraphModelRootObject item = new mxGraphModelRootObject();
                    Type objectType = typeof(mxGraphModelRootObject);

                    if (diagramXml.root.Items[i].GetType() == objectType)
                    {
                        item = (mxGraphModelRootObject) diagramXml.root.Items[i];
                    }

                    if (item.id == vertice.id)
                    {
                        item.label = vertice.label;
                        item.mxCell.style = this.SetImage(vertice.assetType, item.mxCell.style);
                        item.internalLabel = vertice.label;
                        item.HasUniqueQuestions = vertice.HasUniqueQuestions;
                        item.Criticality = vertice.Criticality;
                        item.Description = vertice.Description;
                        item.HostName = vertice.HostName;
                        diagramXml.root.Items[i] = (object)item;
                    }
                }

                SaveDiagramXml(assessmentId, diagramXml);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            finally
            {
            }

            
        }

        /// <summary>
        /// Update fields of zone
        /// </summary>
        /// <param name="vertice"></param>
        /// <param name="assessmentId"></param>
        public void SaveZone(mxGraphModelRootObject vertice, int assessmentId)
        {
            try
            {
                var stream = GetDiagramXml(assessmentId);
                XmlSerializer serializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)serializer.Deserialize(stream);
                for (int i = 0; i < diagramXml.root.Items.Length; i++)
                {
                    mxGraphModelRootObject item = new mxGraphModelRootObject();
                    Type objectType = typeof(mxGraphModelRootObject);

                    if (diagramXml.root.Items[i].GetType() == objectType)
                    {
                        item = (mxGraphModelRootObject)diagramXml.root.Items[i];
                    }

                    if (item.id == vertice.id)
                    {
                        item.label = vertice.label;
                        item.zoneType = vertice.zoneType;
                        item.SAL = vertice.SAL;
                        item.internalLabel = vertice.label;
                        diagramXml.root.Items[i] = (object)item;
                    }
                }

                SaveDiagramXml(assessmentId, diagramXml);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            finally
            {
            }


        }

        /// <summary>
        /// Update fields of shape
        /// </summary>
        /// <param name="vertice"></param>
        /// <param name="assessmentId"></param>
        public void SaveShape(mxGraphModelRootMxCell vertice, int assessmentId)
        {
            try
            {
                var stream = GetDiagramXml(assessmentId);
                XmlSerializer serializer = new XmlSerializer(typeof(mxGraphModel));
                var diagramXml = (mxGraphModel)serializer.Deserialize(stream);
                for (int i = 0; i < diagramXml.root.Items.Length; i++)
                {
                    mxGraphModelRootMxCell item = new mxGraphModelRootMxCell();
                    Type objectType = typeof(mxGraphModelRootMxCell);

                    if (diagramXml.root.Items[i].GetType() == objectType)
                    {
                        item = (mxGraphModelRootMxCell)diagramXml.root.Items[i];
                    }

                    if (item.id == vertice.id)
                    {
                        item.value = vertice.value;
                        diagramXml.root.Items[i] = (object)item;
                    }
                }

                SaveDiagramXml(assessmentId, diagramXml);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
            }
            finally
            {
            }
        }

        public void SaveDiagramXml(int assessmentId, mxGraphModel diagramXml)
        {
            using (var sww = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sww))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(mxGraphModel));
                    serializer.Serialize(writer, diagramXml);
                    string xml = sww.ToString();
                    var assessment = db.ASSESSMENTS.FirstOrDefault(x => x.Assessment_Id == assessmentId);
                    var xDoc = new XmlDocument();
                    xDoc.LoadXml(xml);
                    if (assessment != null)
                        SaveDiagram(assessmentId, xDoc, assessment.LastUsedComponentNumber, assessment.Diagram_Image);
                }
            }
        }

        public string SetImage(string asset, string style)
        {
            var symbols = this.GetAllComponentSymbols();
            var styles = style.Split(';');
            string newStyle = string.Empty;
            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i].Contains("image="))
                {
                    styles[i] = string.Format("image=img/cset/{0}",
                        symbols.FirstOrDefault(x => x.DisplayName == asset)?.FileName);
                }
            }

            foreach (var s in styles)
            {
                newStyle += !string.IsNullOrEmpty(s) ? s + ";": string.Empty;
            }

            return newStyle;
        }

        /// <summary>
        /// Returns all diagram templates stored in the database.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<DiagramTemplate> GetDiagramTemplates()
        {
            var templates = Enumerable.Empty<DiagramTemplate>();
            using (var db = new CSET_Context())
            {
                templates = db.DIAGRAM_TEMPLATES
                    .Where(x => x.Is_Visible ?? false)
                    .OrderBy(x => x.Id)
                    .Select(x => new DiagramTemplate {
                        Name = x.Template_Name,
                        ImageSource = x.Image_Source,
                        Markup = x.Diagram_Markup
                    })
                    .ToArray();
            }
            return templates;
        }
    }

    public class LayerVisibility
    {
        public string layerName { get; set; }
        public string visible { get; set; }

        public string Parent_DrawIo_id { get; set; }
        public string DrawIo_id { get; set; }
        public int Container_Id { get; internal set; }
    }
}