﻿using System.Xml.Linq;
using FasTnT.Domain.Model.Events;
using System;
using FasTnT.Domain.Model.MasterData;
using System.Linq;

namespace FasTnT.Domain.Services.Formatting
{
    public class EventFormatter : IEventFormatter
    {
        public static string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

        public XElement Format(EpcisEvent @event)
        {
            switch (@event.EventType)
            {
                case EventType.Object:
                    return FormatObjectEvent(@event);
                case EventType.Transaction:
                    return FormatTransactionEvent(@event);
                case EventType.Transformation:
                    return FormatTransformationEvent(@event);
            }

            throw new Exception($"Unknown event type: {@event.EventType}");
        }

        private XElement FormatObjectEvent(EpcisEvent @event)
        {
            var element = new XElement("ObjectEvent");

            element.Add(new XElement("eventTime", @event.EventTime.ToString(DateTimeFormat)));
            element.Add(new XElement("recordTime", @event.CaptureTime.ToString(DateTimeFormat)));
            element.Add(new XElement("eventTimeZoneOffset", @event.EventTimezoneOffset.Representation));

            AddEpcList(@event, element);

            element.Add(new XElement("action", @event.Action.ToString().ToUpper()));

            if (!string.IsNullOrEmpty(@event.BusinessStep)) element.Add(new XElement("bizStep", @event.BusinessStep));
            if (!string.IsNullOrEmpty(@event.Disposition)) element.Add(new XElement("disposition", @event.Disposition));

            AddReadPoint(@event, element);
            AddBusinessLocation(@event, element);
            AddBusinessTransactions(@event, element);
            AddIlmd(@event, element);
            AddSourceDest(@event, element);
            AddCustomFields(@event, element);

            return element;
        }

        private XElement FormatTransactionEvent(EpcisEvent @event)
        {
            var element = new XElement("TransactionEvent");

            element.Add(new XElement("eventTime", @event.EventTime.ToString(DateTimeFormat)));
            element.Add(new XElement("recordTime", @event.CaptureTime.ToString(DateTimeFormat)));
            element.Add(new XElement("eventTimeZoneOffset", @event.EventTimezoneOffset.Representation));

            AddEpcList(@event, element);

            element.Add(new XElement("action", @event.Action.ToString().ToUpper()));

            if (!string.IsNullOrEmpty(@event.BusinessStep)) element.Add(new XElement("bizStep", @event.BusinessStep));
            if (!string.IsNullOrEmpty(@event.Disposition)) element.Add(new XElement("disposition", @event.Disposition));

            AddReadPoint(@event, element);
            AddBusinessLocation(@event, element);
            AddBusinessTransactions(@event, element);
            AddIlmd(@event, element);
            AddSourceDest(@event, element);
            AddCustomFields(@event, element);

            return element;
        }

        public XElement FormatTransformationEvent(EpcisEvent epcisEvent)
        {
            var element = new XElement("TransformationEvent");

            element.Add(new XElement("eventTime", epcisEvent.EventTime));
            element.Add(new XElement("recordTime", epcisEvent.CaptureTime));
            element.Add(new XElement("eventTimeZoneOffset", epcisEvent.EventTimezoneOffset.Representation));

            AddEpcList(epcisEvent, element);

            element.Add(new XElement("action", epcisEvent.Action.ToString().ToUpper()));

            if (!string.IsNullOrEmpty(epcisEvent.TransformationId)) element.Add(new XElement("transformationID", epcisEvent.BusinessStep));
            if (!string.IsNullOrEmpty(epcisEvent.BusinessStep)) element.Add(new XElement("bizStep", epcisEvent.BusinessStep));
            if (!string.IsNullOrEmpty(epcisEvent.Disposition)) element.Add(new XElement("disposition", epcisEvent.Disposition));

            AddReadPoint(epcisEvent, element);
            AddBusinessLocation(epcisEvent, element);
            AddCustomFields(epcisEvent, element);

            return element;
        }

        private void AddSourceDest(EpcisEvent epcisEvent, XElement element)
        {
            if (epcisEvent.SourcesDestinations == null || !epcisEvent.SourcesDestinations.Any()) return;

            var source = new XElement("sourceList");
            var destination = new XElement("destinationList");

            foreach (var sourceDest in epcisEvent.SourcesDestinations)
            {
                if (sourceDest.Direction == SourceDestinationType.Source)
                    source.Add(new XElement("source", new XAttribute("type", sourceDest.Type), sourceDest.Id));
                else if (sourceDest.Direction == SourceDestinationType.Destination)
                    destination.Add(new XElement("destination", new XAttribute("type", sourceDest.Type), sourceDest.Id));
            }

            if (source.HasElements) AddInExtension(element, source);
            if (destination.HasElements) AddInExtension(element, destination);
        }

        private static void AddBusinessTransactions(EpcisEvent epcisEvent, XContainer element)
        {
            if (epcisEvent.BusinessTransactions == null || !epcisEvent.BusinessTransactions.Any()) return;

            var transactions = new XElement("bizTransactionList");

            foreach (var trans in epcisEvent.BusinessTransactions)
                transactions.Add(new XElement("bizTransaction", trans.Id, new XAttribute("type", trans.Type)));

            element.Add(transactions);
        }

        private static void AddIlmd(EpcisEvent epcisEvent, XContainer element)
        {
            var elts = epcisEvent.CustomFields.Where(x => x.Type == FieldType.Ilmd).Select(i => new XElement(XName.Get(i.Name, i.Namespace), i.Value));

            if (elts.Any()) AddInExtension(element, new XElement("ilmd", elts));
        }


        private static void AddCustomFields(EpcisEvent epcisEvent, XContainer element)
        {
            foreach (var field in epcisEvent.CustomFields.Where(x => x.Type == FieldType.EventExtension))
                element.Add(new XElement(XName.Get(field.Name, field.Namespace), field.Value));
        }

        // TODO: reformat to match all event types.
        private static void AddEpcList(EpcisEvent epcisEvent, XContainer element)
        {
            var epcList = new XElement("epcList");
            var epcQuantity = new XElement("epcQuantity");
            foreach (var epc in epcisEvent.Epcs.Where(x => x.Type == EpcType.List)) epcList.Add(new XElement("epc", epc.Id));
            foreach (var epc in epcisEvent.Epcs.Where(x => x.Type == EpcType.Quantity))
            {
                var qtyElement = new XElement("quantityElement");
                qtyElement.Add(new XElement("epcClass", epc.Id));
                if (epc.Quantity != null) qtyElement.Add(new XElement("quantity", epc.Quantity));
                if (!string.IsNullOrEmpty(epc.UnitOfMeasure)) qtyElement.Add(new XElement("uom", epc.UnitOfMeasure));

                epcQuantity.Add(qtyElement);
            }

            if (epcList.HasElements) element.Add(epcList);
            if (epcQuantity.HasElements) AddInExtension(element, epcQuantity);
        }

        private static void AddReadPoint(EpcisEvent epcisEvent, XContainer element)
        {
            if (string.IsNullOrEmpty(epcisEvent.ReadPoint)) return;

            var readPoint = new XElement("readPoint", new XElement("id", epcisEvent.ReadPoint));

            foreach (var ext in epcisEvent.CustomFields.Where(x => x.Type == FieldType.ReadPointExtension))
                readPoint.Add(new XElement(XName.Get(ext.Name, ext.Namespace), ext.Value));

            element.Add(readPoint);
        }

        private static void AddBusinessLocation(EpcisEvent epcisEvent, XContainer element)
        {
            if (string.IsNullOrEmpty(epcisEvent.BusinessLocation)) return;

            var custom = epcisEvent.CustomFields.Where(x => x.Type == FieldType.BusinessLocationExtension).Select(field => new XElement(XName.Get(field.Name, field.Namespace), field.Value));
            element.Add(new XElement("bizLocation", new XElement("id", epcisEvent.BusinessLocation), custom));
        }

        private static void AddInExtension(XContainer container, XElement element)
        {
            var extension = container.Element("extension");
            if (extension == null)
            {
                extension = new XElement("extension");
                container.Add(extension);
            }

            extension.Add(element);
        }
    }
}