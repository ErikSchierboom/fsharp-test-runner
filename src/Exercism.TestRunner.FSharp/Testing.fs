module Exercism.TestRunner.FSharp.Testing

open System.Diagnostics
open System.Reflection
open Xunit
open Xunit.Abstractions
open Xunit.Sdk
open Exercism.TestRunner.FSharp.Core
open Exercism.TestRunner.FSharp.Compiler
open FSharp.Compiler.SourceCodeServices

module Process =
    let exec fileName arguments workingDirectory =
        let psi = ProcessStartInfo(fileName, arguments)
        psi.WorkingDirectory <- workingDirectory
        psi.RedirectStandardInput <- true
        psi.RedirectStandardError <- true
        psi.RedirectStandardOutput <- true
        use p = Process.Start(psi)
        p.WaitForExit()

        if p.ExitCode = 0 then Result.Ok() else Result.Error()

module String =
    let normalize (str: string) = str.Replace("\r\n", "\n").Trim()

module Option =
    let ofNonEmptyString (str: string) =
        if System.String.IsNullOrWhiteSpace(str) then None else Some str

let private formatTestOutput output =
    let truncate (str: string) =
        let maxLength = 500

        if str.Length > maxLength
        then sprintf "%s\nOutput was truncated. Please limit to %d chars." str.[0..maxLength] maxLength
        else str

    output
    |> String.normalize
    |> Option.ofNonEmptyString
    |> Option.map truncate

let private testResultFromPass (passedTest: ITestPassed) =
    { Name = passedTest.TestCase.DisplayName
      Status = TestStatus.Pass
      Message = None
      Output = formatTestOutput passedTest.Output }

let private failureToMessage messages =
    messages
    |> Array.map String.normalize
    |> Array.map (fun message ->
        message.Replace("Exception of type 'FsUnit.Xunit+MatchException' was thrown.\n", "").Trim())
    |> String.concat "\n"

let private testResultFromFailed (failedTest: ITestFailed) =
    { Name = failedTest.TestCase.DisplayName
      Status = TestStatus.Fail
      Message = Some(failureToMessage failedTest.Messages)
      Output = formatTestOutput failedTest.Output }

let private runTests (assembly: Assembly) =
    // var command = "dotnet";
    // var arguments = $"test --verbosity=quiet --logger \"trx;LogFileName={Path.GetFileName(_options.TestResultsFilePath)}\" /flp:v=q";

    let assemblyInfo = Reflector.Wrap(assembly)
    let testAssembly = TestAssembly(assemblyInfo)

    let testResults = ResizeArray()
    executionMessageSink.Execution.add_TestFailedEvent (fun args -> testResults.Add(testResultFromFailed (args.Message)))
    executionMessageSink.Execution.add_TestPassedEvent (fun args -> testResults.Add(testResultFromPass (args.Message)))

    let testCases = findTestCases assemblyInfo

    use assemblyRunner =
        createTestAssemblyRunner testCases testAssembly

    assemblyRunner.RunAsync()
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> ignore

    Seq.toList testResults

let private testRunStatusFromTest (tests: TestResult list) =
    let statuses =
        tests |> List.map (fun test -> test.Status) |> set

    if Set.contains Fail statuses then Fail
    elif Set.singleton Pass = statuses then Pass
    else Error

let private testRunFromTests tests =
    { Message = None
      Status = testRunStatusFromTest tests
      Tests = tests }

let private testRunFromError error =
    { Message = Some error
      Status = Error
      Tests = [] }

let private errorToMessage (error: FSharpErrorInfo) =
    let fileName =
        System.IO.Path.GetFileName(error.FileName)

    let lineNumber = error.StartLineAlternate
    let message = String.normalize error.Message

    sprintf "%s:%i: %s" fileName lineNumber message

let private testRunFromTestRunnerError testRunnerError =
    match testRunnerError with
    | BuildError errors ->
        testRunFromError
            (errors
             |> Array.map errorToMessage
             |> String.concat "\n"
             |> String.normalize)
    | TestsFileNotParsed -> testRunFromError "Could not parse test file"
    | ProjectNotFound -> testRunFromError "Could not find project file"
    | TestsFileNotFound -> testRunFromError "Could not find test file"

let private testRunFromCompiledAssembly (assembly: Assembly) = assembly |> runTests |> testRunFromTests

let testRunFromCompilationResult result =
    match result with
    | Result.Ok assembly -> testRunFromCompiledAssembly assembly
    | Result.Error error -> testRunFromTestRunnerError error
