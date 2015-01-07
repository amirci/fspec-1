module FSpec.Matchers

type MatchResult<'T> =
    | MatchSuccess of 'T
    | MatchFail of obj

type MatchType =
    | Should
    | ShouldNot

[<AbstractClass>]
type Matcher<'TActual,'TResult> () = 
    abstract member ApplyActual : (MatchResult<'TResult> -> 'a) -> 'TActual -> 'a
    abstract member ExpectationMsgForShould : string
    abstract member ExpectationMsgForShouldNot : string
    [<System.Obsolete("Use ExpectationMsgForShould instead")>]
    member this.FailureMsgForShould = this.ExpectationMsgForShould
    [<System.Obsolete("Use ExpectationMsgForShouldNot instead")>]
    member this.FailureMsgForShouldNot = this.ExpectationMsgForShouldNot
    static member IsMatch (matcher:Matcher<'TActual,'TResult>) actual =
        let resultToBool = function | MatchSuccess _ -> true | _ -> false
        actual |> matcher.ApplyActual resultToBool
    member self.MessageFor = 
        function
            | Should -> self.ExpectationMsgForShould
            | ShouldNot -> self.ExpectationMsgForShouldNot

let applyMatcher<'T,'U> (matcher: Matcher<'T,_>) f (a : 'T) : 'U =
    matcher.ApplyActual f a

let createFullMatcher<'T,'TResult> 
        (f : 'T -> MatchResult<'TResult>) 
        (shouldMsg : string) 
        (shouldNotMsg : string) =
    { new Matcher<'T,_> () with
        member __.ApplyActual g actual = f actual |> g
        member __.ExpectationMsgForShould = shouldMsg
        member __.ExpectationMsgForShouldNot = shouldNotMsg
    }

let createMatcher<'T,'TResult> (f : 'T -> MatchResult<'TResult>) (shouldMsg : string) =
    createFullMatcher f shouldMsg (sprintf "not %s" shouldMsg)

/// Helps create a matcher, that uses a child matcher for some verification.
/// The passed function should extract the value from the actual value, that
/// the child matcher should match. E.g. for a sequence length matcher, the
/// f extracts the length of the sequence, and the matcher matches the length.
let createCompoundMatcher matcher f =
    createMatcher
        (fun a -> a |> f |> applyMatcher matcher id)

[<System.Obsolete("Use function createCompoundMatcher instead")>]
let createCompountMatcher = createCompoundMatcher

let createFullBoolMatcher<'T> 
        (f : 'T -> bool) 
        (shouldMsg : string) 
        (shouldNotMsg : string) =
    let wrapF = fun a -> 
        match f a with
        | true -> MatchSuccess (a :> obj)
        | false -> MatchFail (a :> obj)
    createFullMatcher wrapF shouldMsg shouldNotMsg

let createBoolMatcher<'T> (f : 'T -> bool) (shouldMsg : string) =
    createFullBoolMatcher f shouldMsg (sprintf "not %s" shouldMsg)

let createSimpleMatcher f = createBoolMatcher f "FAIL"
        
let ( &&& ) (a:Matcher<'a,'b>) (b:Matcher<'a,'b>) =
    let f actual =
        let x = a.ApplyActual id actual
        let y = b.ApplyActual id actual
        match (x,y) with
        | MatchSuccess _, MatchSuccess _ -> MatchSuccess actual
        | _ -> MatchFail actual
    createMatcher f 
        (sprintf "%s and %s" 
            a.ExpectationMsgForShould
            b.ExpectationMsgForShould)

let equal expected =
    createBoolMatcher
        (fun a -> a = expected)
        (sprintf "equal %A" expected)

module be =
    let equalTo = equal

    let ofType<'T> () =
        let f (actual:obj) : MatchResult<'T> =
            match actual with
            | :? 'T as x -> x |> MatchSuccess
            | null -> MatchFail null
            | _ as x -> MatchFail (x.GetType())
        createMatcher f (sprintf "be of type %s" (typeof<'T>.Name))

    let greaterThan expected =
        createBoolMatcher
            (fun a -> a > expected)
            (sprintf "be greater than %A" expected)

    let lessThan expected =
        createBoolMatcher
            (fun a -> a < expected)
            (sprintf "be greater than %A" expected)

    let True =
        createBoolMatcher 
            (fun actual -> actual = true) 
            (sprintf "be true")

    let False =
        createBoolMatcher 
            (fun actual -> actual = false)
            (sprintf "be false")

    module string =
        let containing expected =
            createBoolMatcher
                (fun (a:string) -> a.Contains(expected))
                (sprintf "contain '%s'" expected)

        let matching pattern =
            let regex = System.Text.RegularExpressions.Regex pattern
            createBoolMatcher
                (fun actual -> regex.IsMatch actual)
                (sprintf "match regex pattern %A" pattern)

module have =
    let atLeastOneElement matcher =
        let f a = a |> Seq.exists (Matcher.IsMatch matcher)
        let msg = sprintf "contain at least one element to %s" matcher.ExpectationMsgForShould
        let notMsg = sprintf "contain no elements to %s" matcher.ExpectationMsgForShould
        createFullBoolMatcher f msg notMsg

    let element = atLeastOneElement
    
    let length lengthMatcher =
        createCompoundMatcher
            lengthMatcher
            (fun a -> a |> Seq.length) 
            (sprintf "have length to %s" lengthMatcher.ExpectationMsgForShould) 

    let exactly no matcher =
        let f a = a |> Seq.filter (Matcher.IsMatch matcher) |> Seq.length = no
        let msg = 
            sprintf "contain exactly %d element to %s" no 
                matcher.ExpectationMsgForShould
        createBoolMatcher f msg

let fail =
    let f a =
        try
            a (); false
        with
        | _ -> true
    createBoolMatcher f "fail"

let succeed =
    let f a =
        try
            a (); true
        with
        | _ -> false
    createBoolMatcher f "pass"

module throwException =
    let withMessage matcher =
        let f a = 
            try
                a ()
                MatchFail "No exception thrown"
            with
            | e -> e.Message |> applyMatcher matcher id 
        createMatcher f
            (sprintf "throw exception with message %s" matcher.ExpectationMsgForShould)
            
    let withMessageContaining msg =
        withMessage (be.string.containing msg)
    
let performMatch<'T,'U> matchType (matcher:Matcher<'T,'U>) (actual:'T) =
    let raiseMsg a = 
            let msg = sprintf "%A was expected to %s but was %A" 
                        actual (matcher.MessageFor matchType) a
            raise (AssertionError { Message = msg })
    let continuation result =
        match (matchType, result) with
        | (Should, MatchFail x) -> raiseMsg x
        | (ShouldNot, MatchSuccess x) -> raiseMsg x
        | _ -> ()
    matcher.ApplyActual continuation actual
    
let should<'T,'U> = performMatch<'T,'U> Should 
let shouldNot<'T,'U> = performMatch<'T,'U> ShouldNot

/// Extension methods for System.Object to aid in assertions
type System.Object with
    /// Allows the use of testContext.Subject.Should (matcher)
    member self.Should<'T,'U> (matcher : Matcher<'T,'U>) =
        self :?> 'T |> should matcher

    /// Allows the use of testContext.Subject.ShouldNot (matcher)
    member self.ShouldNot<'T,'U> (matcher : Matcher<'T,'U>) =
        self :?> 'T |> shouldNot matcher

    member self.Apply<'T,'U> (f : 'T -> 'U) =
        self :?> 'T |> f

type Async<'T> with
    member self.Should<'T,'U> (matcher : Matcher<'T,'U>) =
        Async.RunSynchronously(self,5000).Should matcher

    member self.ShouldNot<'T,'U> (matcher : Matcher<'T,'U>) =
        Async.RunSynchronously(self,5000).ShouldNot matcher

let ( >>> ) (a : Matcher<'a,'b>) (b: Matcher<'b,_>) =
    let f actual =
        match a.ApplyActual id actual with
        | MatchSuccess x -> b.ApplyActual id x
        | MatchFail x -> MatchFail x
    createMatcher f (sprintf "%s %s" a.ExpectationMsgForShould b.ExpectationMsgForShould)
