module FSpec.SelfTests.CustomMatchers
open FSpec
open Example
open Matchers
open ExampleGroup

let haveLineMatching pattern = have.element (be.string.matching pattern)

let haveChildGroups expected =
    createSimpleMatcher (fun (actual:ExampleGroup.T<TestContext>) ->
        actual.ChildGroups |> Seq.length = expected)

let haveNoOfExamples expected =
    createSimpleMatcher (fun (actual:ExampleGroup.T<TestContext>) ->
        actual
        |> (fun x -> x.Examples)
        |> Seq.length = expected)

let haveGroupName expected =
    (fun (actual:ExampleGroup.T<TestContext>)-> actual.Name = expected)
    |> createSimpleMatcher

let failWithAssertionError expected =
    let matcher = be.string.containing expected
    let f a = 
        try
            a ()
            MatchFail "No exception thrown"
        with
            | AssertionError(info) -> 
                info.Message |> applyMatcher matcher id
    createMatcher f
        (sprintf "fail assertion with message %A" expected)

module beExampleGroup =
    open ExampleGroup

    let withMetaData name matcher =
        createCompoundMatcher
            matcher
            (fun (a:ExampleGroup.T<TestContext>) -> a.MetaData.Get name)
            (sprintf "have metadata %A to %s" name matcher.FailureMsgForShould)

module beExample =
    open Example
    
    let named expected =
        (fun (actual:Example.T<TestContext>) -> actual.Name = expected)
        |> createSimpleMatcher

