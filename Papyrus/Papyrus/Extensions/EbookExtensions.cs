﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;

namespace Papyrus
{
    internal static class EBookExtensions
    {
        /// <summary>
        /// Gets the location of the content.opf file.
        /// </summary>
        /// <param name="ebook">The EBook for which to get the content location.</param>
        /// <returns>A string location.</returns>
        public static async Task<string> GetContentLocationAsync(this EBook ebook)
        {
            async Task<string> GetContentXmlAsync()
            {
                var folder = await ebook._rootFolder.GetFolderAsync("META-INF");
                var file = await folder.GetFileAsync("container.xml");
                var xml = await FileIO.ReadTextAsync(file);
                return xml;
            }

            XElement GetRootFileNode(string xml)
            {
                var doc = XDocument.Parse(xml);
                var ns = doc.Root.GetDefaultNamespace();
                var node = doc.Element(ns + "container").Element(ns + "rootfiles").Element(ns + "rootfile");
                return node;
            }

            bool VerifyMediaType(XElement node) =>
                node.Attribute("media-type")?.Value == "application/oebps-package+xml";
            
            var containerXml = await GetContentXmlAsync();
            var rootFileNode = GetRootFileNode(containerXml);

            if (!VerifyMediaType(rootFileNode))
                throw new Exception("Invalid media type on rootfile node.");

            return rootFileNode.Attribute("full-path")?.Value;
        }

        /// <summary>
        /// Gets metadata from an EBook.
        /// </summary>
        /// <param name="ebook">The EBook for which to get metadata.</param>
        /// <returns>The metadata object.</returns>
        public static async Task<Metadata> GetMetadataAsync(this EBook ebook)
        {
            XElement GetMetadataNode(string xml)
            {
                var doc = XDocument.Parse(xml);
                var ns = doc.Root.GetDefaultNamespace();
                var node = doc.Element(ns + "package").Element(ns + "metadata");
                return node;
            }

            var contentFile = await ebook._rootFolder.GetFileFromPathAsync(ebook.ContentLocation);
            var contentXml = await FileIO.ReadTextAsync(contentFile);
            var metadataNode = GetMetadataNode(contentXml);
            var dcNamespace = metadataNode.GetNamespaceOfPrefix("dc");

            string GetValue(string node) =>
                metadataNode.Element(dcNamespace + node)?.Value;
            
            var metadata = new Metadata
            {
                AlternativeTitle = GetValue("alternative"),
                Audience = GetValue("audience"),
                Available = GetValue("available") == null ? default(DateTime) : DateTime.Parse(GetValue("available")),
                Contributor = GetValue("contributor"),
                Created = GetValue("created") == null ? default(DateTime) : DateTime.Parse(GetValue("created")),
                Creator = GetValue("creator"),
                Date = GetValue("date") == null ? default(DateTime) : DateTime.Parse(GetValue("date")),
                Description = GetValue("description"),
                Language = GetValue("language"),
                Title = GetValue("title")
            };

            return metadata;
        }

        /// <summary>
        /// Verifies that the EBook has a valid mimetype file.
        /// </summary>
        /// <param name="ebook">The EBook to be checked.</param>
        /// <returns>A bool indicating whether or not the miemtype is valid.</returns>
        public static async Task<bool> VerifyMimetypeAsync(this EBook ebook)
        {
            bool VerifyMimetypeString(string value) =>
                value == "application/epub+zip";

            if (ebook._rootFolder == null)                                      // Make sure a root folder was specified.
                return false;

            var mimetypeFile = await ebook._rootFolder.GetItemAsync("mimetype");

            if (mimetypeFile == null)                                           // Make sure file exists.
                return false;

            var fileContents = await FileIO.ReadTextAsync(mimetypeFile as StorageFile);

            if (!VerifyMimetypeString(fileContents))                         // Make sure file contents are correct.
                return false;

            return true;
        }
    }
}