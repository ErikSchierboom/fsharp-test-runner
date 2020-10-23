module Exercism.TestRunner.FSharp.Core

type TestStatus =
    | Pass
    | Fail
    | Error

type TestResult =
    { Name: string
      Message: string option
      Output: string option
      Status: TestStatus }

type TestRun =
    { Message: string option
      Status: TestStatus
      Tests: TestResult [] }

type TestRunContext =
    { TestsFile: string
      TestResultsFile: string
      BuildLogFile: string
      ResultsFile: string }
