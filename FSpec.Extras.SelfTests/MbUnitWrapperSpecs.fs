﻿module FSpec.Extras.SelfTests.MbUnitWrapperSpecs
open FSpec.Dsl
open FSpec.Runner
open FSpec.Matchers
open FSpec.ExampleGroup
open FSpec.SelfTests.ExampleHelper
open MbUnit.Framework
open FSpec.MbUnitWrapper

module SuiteHelpers =
  module beSuite =
    let withName m =
      createCompoundMatcher m (fun (x:TestSuite) -> x.Name) "have name"
    let withChildren m =
      createCompoundMatcher m (fun (x:TestSuite) -> x.Children) "have children"

  module beExample =
    let withWrappedExample m =
      createCompoundMatcher m (fun (x:FSpecTestCase) -> x.WrappedExample) "have wrapped example"

open SuiteHelpers

type Wrapper() =
  inherit MbUnitWrapperBase()

let specs =
  describe "MbUnit wrapper" [
    describe "createSuiteFromExampleGroup()" [
      context "with a 'slow' example" [
        subject <| fun _ -> 
            anExampleGroupNamed "Group"
            |> withExamples [ aSlowExample ]
            |> (fun x -> [x])
            |> createSuitesFromExampleGroups
            |> List.map (fun x -> x :?> TestSuite)

        it "creates an empty suite" (fun ctx ->
          ctx.Subject.Should (have.length (be.equalTo 0))
        )
      ]

      context "with a 'focus' example" [
        subject <| fun _ -> 
            anExampleGroupNamed "Group"
            |> withExamples [ anExample; aFocusedExample ]
            |> (fun x -> [x])
            |> createSuitesFromExampleGroups
            |> List.map (fun x -> x :?> TestSuite)

        it "only executes the focused example" (fun ctx -> 
          ctx.Subject.Should (have.exactly 1 (beSuite.withChildren (have.length (be.equalTo 1))))
        )
      ]

      context "with an example group" [
        before <| fun ctx -> ctx?group <- anExampleGroupNamed "Group"
        subject (fun c -> c?group |> createSuiteFromExampleGroup)

        context "with an example" [
          before (fun c -> 
            let example = anExampleNamed "Example"
            c?example <- example
            c?group <- c?group |> withExamples [ example ])

          it "Creates a test suite named 'Group'" <| fun ctx ->
            ctx.Subject.Should (beSuite.withName (equal "Group"))

          it "Creates a test suite with an example" <| fun ctx ->
            let suite = ctx.GetSubject<TestSuite> ()
            let testCase : TestCase = suite.Children |> Seq.exactlyOne :?> TestCase
            testCase.Name.Should (equal "Example")

          describe "constructed example" [
            subject (fun c -> 
              let suite = c.GetSubject<TestSuite>()
              suite.Children |> Seq.exactlyOne)

            it "should reference actual example" <| fun c ->
              let expected = {
                Example = c?example
                ContainingGroups = [c?group] }
              c.Subject.Should (beExample.withWrappedExample (equal expected))
          ]
        ]

        context "with a child group" [
          before (fun c ->
            let example = anExampleNamed "Example"
            c?example <- example
            c?group <- c?group |> (withNestedGroupNamed "Child" (withExamples [example])))

          it "Creates a suite with nested suite" <| fun ctx ->
            let suite = ctx.GetSubject<TestSuite> ()
            let childSuite = suite.Children |> Seq.exactlyOne :?> TestSuite
            childSuite.Should (beSuite.withName (equal "Child"))

          it "Initializes all parent groups" <| fun c ->
            let childSuite =
              c.GetSubject<TestSuite> ()
              |> fun x -> x.Children.[0] :?> TestSuite
            let testCase = childSuite.Children.[0]
            let childGroup = c?group |> fun x -> x.ChildGroups |> Seq.exactlyOne
            let expected = {
              Example = c?example
              ContainingGroups = [childGroup; c?group] }
            testCase.Should (beExample.withWrappedExample (equal expected))
        ]
      ]
    ]
  ]