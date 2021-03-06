FSpec
=====

_RSpec inspired test framework for F#_

The aim of this project is to provide a test framework to the .NET platform
having the same elegance as the RSpec framework has on Ruby.

You can easily use this framework to test C# code - that was in fact the
original intent for this project.

Currently the following features are supported

 * Nested example groups/contexts
 * Setup/teardown in example groups
 * Test context - can be used to pass data from setup to test
 * Metadata on individual examples or example groups (accessible from setup)
 * Assertion framework
 * Implicit subject
 * Automatically disposing _IDisposable_ instances
 * Support for missing metadata, i.e. test context can try to retrieve meta
   data that may or may not have been initialized.
 * Better error messages when context/meta data does not exist, or is of
   incorrect type.
 * One liner examples
 * [Visual Studio Integration](#visual-studio-integration)

Ideas for future improvements

 * Context data and meta data keys can be other types than strings, e.g.
   discriminated unions, partly to avoid name clashes.
 * Global setup/teardown code, useful for clearing database between tests.

The framework is self testing, i.e. the framework is used to test itself.

I have written a few [blog posts][1] about FSpec

#### Status 
 * FSpec: [![NuGet Status](http://img.shields.io/nuget/v/FSpec.svg)](https://www.nuget.org/packages/FSpec/)
 * FSpec.AutoFoq: [![NuGet Status](http://img.shields.io/nuget/v/FSpec.AutoFoq.svg)](https://www.nuget.org/packages/FSpec.AutoFoq/)
 * FSpec.MbUnitWrapper: [![NuGet Status](http://img.shields.io/nuget/v/FSpec.AutoFoq.svg)](https://www.nuget.org/packages/FSpec.MbUnitWrapper/)

[1]: http://stroiman.com/software/fspec

## Want to contribute? ##

Please. Contributers are greatly welcome.

Feedback is also more than welcome.

## Stability ##

FSpec is stil in it's 0.x phase, so there is a risk that the API could change.

However, both the example building DSL, and the matchers have undergone
significant changes already, and I have reached a point where I am quite happy
with most of it. 

So I believe that there should be no major changes to the API.

## General syntax ##

Create an assembly containing your specs. Place your specs in a module, and
assign the spec code to the value _spec_. Pass the name of the assembly to
the fspec runner command line.

```fsharp
module MySpecModule
open FSpec.Dsl

let specs =
    describe "Some component" [
        describe "Some feature" [
            context "In some context" [
                it "has some specific behaviour" (fun _ ->
                    ()
                )
                it "has some other specific behavior" (fun _ ->
                    ()
                )
            ]
            context "In some other context" [
                it "has some completely different behavior" (fun _ ->
                    ()
                )
            ]]]
```

If you find the paranthesis noisy, you can use the backward pipe operator

```fsharp
let specs =
    describe "Some feature" [
        context "In some context" [
            it "has some specific behaviour" <| fun _ ->
                ()
            it "has some other specific behaviour" <| fun _ ->
                ()
```

### Pending tests ###

You can use the function _pending_ as the test body to indicate a test that
needs to be written.

```fsharp
let specs =
    describe "Some feature" [
        it "has some specific behaviour" pending
        it "has some other specific behaviour" pending
    ]
```

This allows you to quickly describe required functionality without being forced
to write a full test up front. 

The test runner reports pending tests, thus you will know if you have more work
to do before the feature is complete. The test runner will not fail because of
pending tests.

### General setup/teardown code ###

The functions _before_/_after_ can be used to hold general setup/teardown code.

```fsharp
let specs =
    describe "Some feature" <| fun _ ->
        before <| fun _ ->
            ()
        after <| fun _ ->
            ()
        it "Has some behavior" <| fun _ ->
            ()
```

## Test Context ##

Setup, teardown, and test functions all receive a _TestContext_ parameter. Test
metadata can be received from this context, and specific test data can be
stored in the context. The latter is useful if the test needs to use data
created in the setup.

The context data is accessible using the ? operator.

```fsharp
let specs =
    describe "createUser function" [
        before <| fun _ ->
            ctx?user <- createUser "John" "Doe"
        it "sets the first name" <| fun ctx ->
            ctx?user |> (fun x -> x.FirstName) |> should equal "John"
        it "sets the last name" <| fun ctx ->
            ctx?user |> (fun x -> x.LastName) |> should equal "Doe"
    ]
```

Where other test frameworks relies on class member fields to share data
between, e.g. general setup and test code, the _TestContext_ is the place to
store it in FSpec.

The data is internally stored as instances of type _obj_, but the ? operator
works with the type inference system, so it will automatically cast the data to
the expected type. 

In the above example, the _fun x -> x.FirstName_ would be inferred to be of
type _User -> string_ - assuming the _User_ type was the only type with a
_FirstName_ property currently opened.

The above example could be written a little nicer, if we introduced custom
matchers for members on the _User_ type

```fsharp
let specs =
    describe "createUser function" [
        before <| fun _ ->
            ctx?user <- createUser "John" "Doe"
        it "sets the first name" <| fun ctx ->
            ctx?user.Should (haveFirstName "John")
        it "sets the last name" <| fun ctx ->
            ctx?user.Should (haveLastName "Doe")
    ]
```

Because the matchers themselves are typed to the type of the expected value,
the type inference system will bring the expected type to the _?_ operator.

If you get a compiler error saying that the it cannot infer the type, use
the generic _Get<'T>_ function instead.

### Automatically Disposal ###

If you add an object that implements _IDisposable_ to the test context, the
object will automatically be disposed when the test run has finished. 

```fsharp
let specs =
    describe "The data access layer" [
        before (fun ctx -> ctx?connection <- createDatabaseConnection () )

        it "uses the connection" ...
    ]
```

The database connection will in this case automatically be disposed.

```fsharp
let specs =
    describe "createUser function" [
        before <| fun _ ->
            ctx?user = createUser "John" "Doe"
        it "sets the first name" <| fun ctx ->
            let user = ctx.Get<User> "user"
            user.FirstName |> should equal "John"
    ]
```
  
### Implicit subject ###

A special context variable, _Subject_, can be used to reference the thing under
test. The variable is of type _obj_, but the generic function _GetSubject<'T>_
will cast the subject to the expected type

```fsharp
let specs =
    describe "createUser function" [
        subject <| fun _ -> createuser "john" "doe"

        it "sets the first name" <| fun ctx ->
            let user = ctx.GetSubject<User> ()
            user.FirstName |> should (equal "John")
        it "sets the last name" <| fun ctx ->
            let user = ctx.GetSubject<User> ()
            user.LastName |> should (equal "Doe")
    ]
```

There are two extension methods declared on _obj_: _Should_, and _ShouldNot_.
These will automatically cast the subject to the type expected by the matcher.

```fsharp
let specs =
    describe "createUser function" [
        subject <| fun _ -> createuser "john" "doe"

        it "sets the first name" <| fun ctx ->
            ctx.Subject.Should (haveFirstName "John")

        it "sets the last name" <| fun ctx ->
            ctx.Subject.Should (haveLastName "Doe")
    ]
```

If you don't have custom matchers for the properties on the subject, there is a
third option _Apply_ which allows you pass a function to retrieve the data from
the subject that is of interest.

```fsharp
let specs =
    describe "createUser function" [
        subject <| fun _ -> createuser "john" "doe"

        it "sets the first name" <| fun ctx ->
            ctx.Subject.Apply (fun x -> x.FirstName)
            |> should (equal "John")

        it "sets the last name" <| fun ctx ->
            ctx.Subject.Apply (fun x -> x.LastName)
            |> should (equal "Doe")
    ]
```

#### Functions as subjects ####
The subject can also be a function.

```fsharp
let specs =
    describe "createUser function, when user already exists" [
        ...
        subject <| fun _ -> 
            (fun () -> CreateAndSaveNewUser())

        it "should fail" <| fun ctx ->
            ctx.Subject.Should fail
```

### Test metadata ###

You can associate metadata to an individual example, or an example group. The
syntax is currently a strange set of operators. The metadata is basically a map
of _string_ keys and _obj_ values.

Meta data assigned to an example, or example group, will be available on the
_TestContext_ when executing the example.

Metadata can be useful when you want to modify a general setup in a more
specific context.

```fsharp
let specs =
    describe "Register new user feature" [
        before (fun ctx ->
            let user = ctx?existing_user
            Mock<IUserRepository>()
                .Setup(fun x -> <@ x.FindByEmail(email) @>)
                .Returns(user)
                .Create()
            |> // do something with the mock
        )

        ("existing_user" ++ null) ==>
        context "when no user exists" [
            it "succeeds" (...)
        ]

        ("existing_user", createValidUser()) **>
        context "when a user has already been registered with that email" [
            it "fails" (...)
        ]

        context "an example with many pieces of metadata" [
            ("data1", 42) **>
            ("data2", "Yummy") **>
            ("data3", Some [1;2;3]) **>
            it "can easily specify a lot of metadata" (fun _ -> ())
        ]
    ]
```

The funny looking _**>_ operator is chosen because it is right-to-left
associative, allowing us to reduce the required no of parenthesis.

The metadata getter is generic, but will fail at runtime if the actual data is
not of the correct type.

Metadata with the same name on a child example group will override the value of
the parent group, and metadata on an example will override that of the group.

## Assertion framework ##

FSpec has it's own set of assertions (not very complete currently). The
assertions are typed, so the actual value must be of the correct type

```fsharp
5 |> should (equal 5) // pass
5 |> should (be.greaterThan 6) // fail
5 |> should (equal 5.0) // does not compile, incompatible types
"foobar" |> should (be.string.matching "ooba") // pass
```

### Writing new assertions ###

Is possible, and soon to be documented.

## Extending Test Context ##

Common test functionality can be created by writing extensions to the
TestContext type. E.g. here, where FSpec is used to test a C# project following
a typical dependency injection architecture.

```fsharp
module MyApplicationSpecs.TestHelpers
open FSpec.Core

type TestContext with
    member self.AutoMocker =
	      self.GetOrDefault "auto_mocker" (fun _ -> AutoMocker())
    member self.GetMock<'T> () = self.AutoMocker.GetMock<'T> ()
    member self.Get<'T> () = self.AutoMocker.Get<'T> ()
```

Open the _TestHelpers_ module from any test module where you need to test a
component with mocked dependencies, and you will have access to the _Get<'T>()_
and _GetMock<'T>()_ methods.

The _GetOrDefault<'T>_ calls the function to initialize a value if it hasn't
already been initialized, otherwise the value stored in the context is 
returned.

### Batch building examples ###

Because examples are created at runtime, you can use _List_ operations to
generate batches of test cases.

```fsharp
let specs =
    describe "Email validator" (
        yield! (["user@example.com"
          ...
          "dotted.user@example.com"]
         |> List.map (fun email ->
            it (sprintf "validates email: %s" email) (fun _ ->
                email |> validateEmail |> should equal true))
        )

        yield! (["user@example";
          ...
          "user@.com"]
         |> List.map (fun email ->
            it (sprintf "does not validate email: %s" email) (fun _ ->
                email |> validateEmail |> should equal false))
        )
    )
```

Although this example is a bit noisy with paranthesis, it shows that it can
be done with the current api.

Alternately, you can create helper functions to create tests for you.

```fsharp
let itIsValidEmail email =
    it (sprintf "validates email: %s" email) (fun _ ->
        email |> validateEmail |> should (equal true))

let itIsInvalidEmail email =
    it (sprintf "does not validate email: %s" email) (fun _ ->
        email |> validateEmail |> should (equal false))

let specs =
    describe "Email validator" (
        itIsValidEmail "user@example.com"
        ...
        itIsValidEmail "dotted.user@example.com"

        itIsInvalidEmail "user@example"
        ...
        itIsInvalidEmail "user@.com"
    )
```
## Running the tests ##

You can either use the console runner for running the specs. Or - create your
spec assembly as a console application, and use this following piece of code as
the main function

```fsharp
[<EntryPoint>]
let main argv = 
    System.Reflection.Assembly.GetExecutingAssembly ()
    |> FSpec.TestDiscovery.runSingleAssembly
```

## Visual Studio Integration ##

You can use the NCrunch plugin for Visual Studio to run unit tests
automatically as you are writing code.

The integration is based on the fact that `MbUnit` allows the creation of a
`DynamicTestFactory` that can return tests as instances of a `TestCase` class.

FSpec provides a base class that contains wrapping code, mapping FSpec to
MbUnit dynamic tests suites.

In the spec assembly, add the `FSpec.MbUnitWrapper` nuget package, and create a
class that derives from `MbUnitWrapperBase`.

```fsharp
[<MbUnit.Framework.TestFixtureAttribute>]
type Wrapper() =
    inherit FSpec.MbUnitWrapper.MbUnitWrapperBase()
```

Just make sure that you have enabled MbUnit support with NCrunch. I am also not
sure whether or not it works in NCrunch version 1.

Unfortunately, NCrunch cannot see each individual test, it only recognizes the
entire test suite as a single test. But if your tests are fast, that should
work for a normal red/green/refactor workflow.
