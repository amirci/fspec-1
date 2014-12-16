﻿module XmlHelpers
open FSpec.Matchers
open System.Xml
open System.Xml.Schema
open System.Reflection

let assembly = Assembly.GetExecutingAssembly()
let resourceName = "JUnit.xsd"
let openSchemaStream () = assembly.GetManifestResourceStream(resourceName)

let validateJUnitXml xml =
  let messages = ref []
  let eventHandler (sender:obj) (e:ValidationEventArgs) =
    let invalidate () =
      messages := e.Message :: !messages
      printfn "XML error: %s" e.Message
    match e.Severity with
    | XmlSeverityType.Error -> invalidate()
    | XmlSeverityType.Warning -> invalidate()
    | _ -> ()
  let document = new XmlDocument()
  let schemaStream = openSchemaStream()
  let reader = XmlReader.Create(schemaStream)
  document.LoadXml xml
  document.Schemas.Add("", reader) |> ignore
  document.Validate(new ValidationEventHandler(eventHandler))
  !messages

let beValidJUnitXml =
  let f actual =
    let issues = validateJUnitXml actual
    match issues with
    | [] -> MatchSuccess ""
    | _ -> MatchFail issues
  createMatcher f "be valid JUnit xml"
