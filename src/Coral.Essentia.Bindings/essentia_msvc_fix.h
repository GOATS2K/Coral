// This file is used as a force-include by the MSVC compiler.
// Its purpose is to load the standard C time header before any
// conflicting Essentia headers are processed, which resolves
// the persistent C2039 and C2873 errors related to <ctime>.
#include <time.h>
