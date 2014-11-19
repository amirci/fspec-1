﻿module FSpec.Dsl
open Matchers

let pending = fun _ -> raise PendingError

type Operation =
    | AddExampleOperation of Example.T<TestContext>
    | AddExampleGroupOperation of ExampleGroup.T<TestContext>
    | AddSetupOperation of TestFunc<TestContext>
    | AddTearDownOperation of TestFunc<TestContext>
    | MultipleOperations of Operation list
    | AddMetaDataOperation of string*obj
    static member ApplyMetaData metaData op =
        match op with
        | AddExampleOperation e ->
            e |> Example.addMetaData metaData |> AddExampleOperation
        | AddExampleGroupOperation g ->
            g |> ExampleGroup.addMetaData metaData |> AddExampleGroupOperation
        | _ -> failwith "not supported"
    static member (==>) (md, op) = Operation.ApplyMetaData md op

let it name func = AddExampleOperation <| Example.create name func

let exampleFromMatcher<'T> matchType (matcher : Matcher<'T>) =
    Example.create 
        (sprintf "should %s" (matcher.MessageFor matchType))
        (fun (ctx:TestContext) -> ctx.Subject.Apply (performMatch matchType matcher))
    |> AddExampleOperation

let itShould<'T> = exampleFromMatcher<'T> Should
let itShouldNot<'T> = exampleFromMatcher<'T> ShouldNot
let describe name operations =
    let rec applyOperation (grp,md) op =
        match op with
        | AddExampleOperation example -> 
            let example = example |> Example.addMetaData (TestDataMap.create md)
            let grp = grp |> ExampleGroup.addExample example
            (grp,[])
        | AddExampleGroupOperation childGrp -> 
            let cg = childGrp |> ExampleGroup.addMetaData (TestDataMap.create md)
            let grp = grp |> ExampleGroup.addChildGroup cg
            (grp,[])
        | AddSetupOperation f -> 
            let grp = grp |> ExampleGroup.addSetup f
            (grp,md)
        | AddTearDownOperation f -> 
            let grp = grp |> ExampleGroup.addTearDown f
            (grp,md)
        | MultipleOperations o -> 
            o |> List.fold applyOperation (grp,md)
        | AddMetaDataOperation (k,v) -> (grp, (k,v)::md)

    let grp = ExampleGroup.create name
    operations |> List.fold applyOperation (grp,[])
    |> fun (grp,_) -> grp
    |> AddExampleGroupOperation
    
let context = describe
let before f = AddSetupOperation f
let after f = AddTearDownOperation f
let subject f = before (fun ctx -> ctx.SetSubject f)
let examples x = MultipleOperations x
let behavior x = MultipleOperations x

let (++) = TestDataMap.(++)
let (<<-) a b = AddMetaDataOperation(a,b)
