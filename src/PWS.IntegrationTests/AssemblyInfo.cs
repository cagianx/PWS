// I test di fixture diverse girano in parallelo: ogni test lavora in una propria
// directory temporanea e ogni server HTTP ascolta su una porta libera dedicata.
// Vedi my-docs/docs/tecnologie/csharp/test-integrazione/02-scrivere-test.md (Parallelismo).
[assembly: Parallelizable(ParallelScope.Fixtures)]
