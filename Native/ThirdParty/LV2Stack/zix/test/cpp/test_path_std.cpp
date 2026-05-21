// Copyright 2020-2022 David Robillard <d@drobilla.net>
// SPDX-License-Identifier: ISC

/*
  Tests that compare results with std::filesystem::path.  Also serves as a
  handy record of divergence between zix, the C++ standard library, and
  different implementations of it.  The inconsistencies are confined to
  Windows.
*/

#undef NDEBUG

#include <zix/path.h>
#include <zix/string_view.h>

#include <cassert>
#include <cstdlib>
#include <filesystem>
#include <string>

// IWYU pragma: no_include <version>

namespace {

struct BinaryCase {
  const char* lhs;
  const char* rhs;
};

const BinaryCase joins[] = {
  {"", ""},           {"", "/b/"},      {"", "b"},         {"/", ""},
  {"..", ".."},       {"..", "name"},   {"..", "/"},       {"/", ".."},
  {"/", nullptr},     {"//host", ""},   {"//host", "a"},   {"//host/", "a"},
  {"/a", ""},         {"/a", "/b"},     {"/a", "/b/"},     {"/a", "b"},
  {"/a", "b/"},       {"/a", nullptr},  {"/a/", ""},       {"/a/", "b"},
  {"/a/", "b/"},      {"/a/", nullptr}, {"/a/b", nullptr}, {"/a/b/", ""},
  {"/a/b/", nullptr}, {"/a/c", "b"},    {"/a/c/", "/b/d"}, {"/a/c/", "b"},
  {"C:", ""},         {"C:/a", "/b"},   {"C:/a", "C:/b"},  {"C:/a", "C:b"},
  {"C:/a", "D:b"},    {"C:/a", "b"},    {"C:/a", "b/"},    {"C:/a", "c:/b"},
  {"C:/a", "c:b"},    {"C:\\a", "b"},   {"C:\\a", "b\\"},  {"C:a", "/b"},
  {"C:a", "C:/b"},    {"C:a", "C:\\b"}, {"C:a", "C:b"},    {"C:a", "b"},
  {"C:a", "c:/b"},    {"C:a", "c:\\b"}, {"C:a", "c:b"},    {"a", ""},
  {"a", "/b"},        {"a", "C:"},      {"a", "C:/b"},     {"a", "C:\\b"},
  {"a", "\\b"},       {"a", "b"},       {"a", "b/"},       {"a", nullptr},
  {"a/", ""},         {"a/", "/b"},     {"a/", "b"},       {"a/", "b/"},
  {"a/", nullptr},    {"a/b", ""},      {"a/b", nullptr},  {"a/b/", ""},
  {"a/b/", nullptr},  {"a\\", "\\b"},   {"a\\", "b"},      {nullptr, "/b"},
  {nullptr, "/b/c"},  {nullptr, "b"},   {nullptr, "b/c"},  {nullptr, nullptr},
};

const BinaryCase lexical_relatives[] = {
  {"", ""},
  {"", "."},
  {".", "."},
  {"../", "../"},
  {"../", "./"},
  {"../", "a"},
  {"../../a", "../b"},
  {"../a", "../b"},
  {"/", "/a/b"},
  {"/", "/a/b/c"},
  {"/", "a"},
  {"/", "a/b"},
  {"/", "a/b/c"},
  {"//host", "//host"},
  {"//host/", "//host/"},
  {"//host/a/b", "//host/a/b"},
  {"/a", "/b/c/"},
  {"/a/", "/a/b"},
  {"/a/", "/b/c"},
  {"/a/", "/b/c/"},
  {"/a/", "b"},
  {"/a/", "b/c/"},
  {"/a/b", "a/b"},
  {"/a/b/c", "/a/b/d/"},
  {"/a/b/c", "/a/b/d/e/"},
  {"/a/b/c", "/a/d"},
  {"C:", "C:a.txt"},
  {"C:", "D:a.txt"},
  {"C:../", "C:../"},
  {"C:../", "C:./"},
  {"C:../", "C:a"},
  {"C:../../a", "C:../b"},
  {"C:../a", "C:../b"},
  {"C:/", "C:a.txt"},
  {"C:/", "D:a.txt"},
  {"C:/D/", "C:F"},
  {"C:/D/", "C:F.txt"},
  {"C:/D/S/", "F"},
  {"C:/D/S/", "F.txt"},
  {"C:/Dir/", "C:/Dir/File.txt"},
  {"C:/Dir/", "C:File.txt"},
  {"C:/Dir/File.txt", "C:/Dir/Sub/"},
  {"C:/Dir/File.txt", "C:/Other.txt"},
  {"C:/Dir/Sub/", "File.txt"},
  {"C:/a.txt", "C:/b/c.txt"},
  {"C:/a/", "C:/a/b.txt"},
  {"C:/a/", "C:b.txt"},
  {"C:/a/b.txt", "C:/a/b/"},
  {"C:/a/b.txt", "C:/d.txt"},
  {"C:/a/b/", "d.txt"},
  {"C:/b/", "C:a.txt"},
  {"C:/b/c/", "a.txt"},
  {"C:F", "D:G"},
  {"C:File.txt", "D:Other.txt"},
  {"C:a.txt", "C:b.txt"},
  {"C:a.txt", "D:"},
  {"C:a.txt", "D:/"},
  {"C:a.txt", "D:e.txt"},
  {"C:a/b/c", "C:../"},
  {"C:a/b/c", "C:../../"},
  {"a", "a"},
  {"a", "a/b/c"},
  {"a", "b/c"},
  {"a/b", "a/b"},
  {"a/b", "c/d"},
  {"a/b/c", "../"},
  {"a/b/c", "../../"},
  {"a/b/c", "a/b/c"},
  {"a/b/c", "a/b/c/x/y"},
  {"a/b/c/", "a/b/c/"},

// Network paths that aren't supported by MinGW
#if !(defined(_WIN32) && defined(__GLIBCXX__))
  {"//host", "//host/"},
  {"//host", "a"},
  {"//host", "a"},
  {"//host/", "a"},
  {"//host/", "a"},
#endif
};

const char* const paths[] = {
  // Valid paths handled consistently on all platforms
  "",
  ".",
  "..",
  "../",
  "../..",
  "../../",
  "../../a",
  "../a",
  "../name",
  "..\\..\\a",
  "..\\a",
  "..name",
  "./",
  "./.",
  "./..",
  ".//a//.//./b/.//",
  "./a/././b/./",
  "./name",
  ".hidden",
  ".hidden.txt",
  "/",
  "/.",
  "/..",
  "/../",
  "/../..",
  "/../../",
  "/../a",
  "/../a/../..",
  "//",
  "///",
  "///dir/",
  "///dir///",
  "///dir///name",
  "///dir///sub/////",
  "///name",
  "/a",
  "/a/",
  "/a/b",
  "/a/b/",
  "/a\\",
  "/a\\b",
  "/a\\b/",
  "/dir/",
  "/dir/.",
  "/dir/..",
  "/dir/../..",
  "/dir/.hidden",
  "/dir//name",
  "/dir//sub/suub/",
  "/dir/name",
  "/dir/name.tar.gz",
  "/dir/name.txt",
  "/dir/name\\",
  "/dir/sub/",
  "/dir/sub/./",
  "/dir/sub//",
  "/dir/sub///",
  "/dir/sub//name",
  "/dir/sub/name",
  "/dir/sub/suub/",
  "/dir/sub/suub/../",
  "/dir/sub/suub/../d",
  "/dir/sub/suub/../d/",
  "/dir/sub/suub/suuub/",
  "/dir/sub\\name",
  "/name",
  "/name.",
  "/name.txt",
  "/name.txt.",
  "C:",
  "C:.",
  "C:..",
  "C:/",
  "C:/.",
  "C:/..",
  "C:/../../name",
  "C:/../name",
  "C:/..dir/..name",
  "C:/..name",
  "C:/./name",
  "C:/.dir/../name",
  "C:/.dir/.hidden",
  "C:/.hidden",
  "C:/dir/",
  "C:/dir/.",
  "C:/dir/..",
  "C:/dir/name",
  "C:/dir/sub/",
  "C:/dir/sub/name",
  "C:/dir/sub/suub/",
  "C:/dir/sub/suub/suuub/",
  "C:/name",
  "C:/name.",
  "C:/name.txt",
  "C:/name\\horror",
  "C:/name\\horror/",
  "C:\\",
  "C:\\a",
  "C:\\a/",
  "C:\\a/b",
  "C:\\a/b\\",
  "C:\\a\\",
  "C:\\a\\b",
  "C:\\a\\b\\",
  "C:\\a\\b\\c",
  "C:\\a\\b\\d\\",
  "C:\\a\\b\\d\\e\\",
  "C:\\b",
  "C:\\b\\c\\",
  "C:a",
  "C:dir/",
  "C:dir/name",
  "C:dir\\",
  "C:name",
  "C:name/",
  "C:name\\horror",
  "D|\\dir\\name",
  "D|\\name",
  "D|name",
  "Z:/a/b",
  "Z:\\a\\b",
  "Z:b",
  "\\",
  "\\a\\/b\\/c\\",
  "\\a\\b\\c\\",
  "\\b",
  "a",
  "a.",
  "a..txt",
  "a.txt",
  "a/b",
  "a/b\\",
  "c:/name",
  "c:\\name",
  "c:name",
  "dir/",
  "dir/.",
  "dir/..",
  "dir/../",
  "dir/../b",
  "dir/../b/../..///",
  "dir/../b/..//..///../",
  "dir/../sub/../name",
  "dir/./",
  "dir/.///b/../",
  "dir/./b",
  "dir/./b/.",
  "dir/./b/..",
  "dir/./sub/./name",
  "dir///b",
  "dir//b",
  "dir/\\b",
  "dir/b",
  "dir/b.",
  "dir/name",
  "dir/name.",
  "dir/name.tar.gz",
  "dir/name.txt",
  "dir/name.txt.",
  "dir/name\\with\\backslashes",
  "dir/sub/",
  "dir/sub/.",
  "dir/sub/..",
  "dir/sub/../",
  "dir/sub/../..",
  "dir/sub/../../",
  "dir/sub/../name",
  "dir/sub/./",
  "dir/sub/./..",
  "dir/sub/./../",
  "dir/sub/./name",
  "dir/sub//",
  "dir/sub///",
  "dir/sub/name",
  "dir/sub/suub/../..",
  "dir/sub/suub/../../",
  "dir/weird<sub/weird%name",
  "dir\\",
  "dir\\/\\a",
  "dir\\/a",
  "dir\\a",
  "dir\\a/",
  "name",
  "name-snakey",
  "name.",
  "name..",
  "name.txt",
  "name.txt.",
  "name/",
  "name//",
  "name///",
  "name_sneaky",

  // Filenames with colons that are handled consistently everywhere
  "C:/a/C:/b",
  "C:/a/c:/b",
  "C:a/C:/b",
  "C:a/C:\\b",
  "C:a/c:/b",
  "C:a/c:\\b",

#ifndef _WIN32
  // Filenames with colons that aren't handled consistently on Windows
  "C:/a/C:b",
  "C:/a/D:b",
  "C:/a/c:b",
  "C:a/C:b",
  "C:a/c:b",
  "NO:DRIVE",
  "NODRIVE:",
  "dir/C:",
  "dir/C:/name",
  "dir/C:\\name",
  "no:drive",
  "nodrive:",
#endif

#ifndef _WIN32
  // Invalid root and network paths that aren't handled consistently on Windows
  "//a//",
  "//host/dir/../../../a",
  "//host/dir/../../a",
  "C://",
#endif

#if !(defined(_WIN32) && defined(__GLIBCXX__))
  // Network paths that aren't supported by MinGW
  "//h",
  "//h/",
  "//h/a",
  "//h/a.txt",
  "//h/d/a.txt",
  "//h/d/s/a.txt",
  "//host",
  "//host/",
  "//host/.",
  "//host/..",
  "//host/../dir/",
  "//host/../name",
  "//host/..dir/",
  "//host/..name",
  "//host/./dir/",
  "//host/./name",
  "//host/.dir/",
  "//host/.name",
  "//host/C:/dir/name.txt",
  "//host/C:/name",
  "//host/dir/.",
  "//host/dir/..",
  "//host/dir/../name",
  "//host/dir/./name",
  "//host/dir/name",
  "//host/dir/sub/name",
  "//host/name",
  "//host/name.",
  "//host/name..",
  "//host\\",
  "//host\\name.txt",
  "\\\\host",
  "\\\\host\\",
  "\\\\host\\C:\\name",
  "\\\\host\\dir\\name.txt",
  "\\\\host\\name.txt",
#endif
};

/// Abort if `path` doesn't match `result` when loosely parsed
bool
match(const std::filesystem::path& path, char* const result)
{
  const bool success = (path.empty() && !result) || (result && path == result);

  free(result);
  return success;
}

/// Abort if `path` doesn't equal `result`
bool
equal(const std::filesystem::path& path, char* const result)
{
  const bool success =
    (path.empty() && !result) || (result && path.u8string() == result);

  free(result);
  return success;
}

/// Abort if `path` doesn't match `view` when loosely parsed
bool
match(const std::filesystem::path& path, const ZixStringView view)
{
  return (path.empty() && !view.length) ||
         (view.length && path == std::string{view.data, view.length});
}

/// Abort if `path` doesn't equal `view`
bool
equal(const std::filesystem::path& path, const ZixStringView view)
{
  return (path.empty() && !view.length) ||
         (view.length &&
          path.u8string() == std::string{view.data, view.length});
}

void
run()
{
  using Path = std::filesystem::path;

  for (const char* string : paths) {
    const Path path{string};

    // Decomposition
    assert(equal(path.root_name(), zix_path_root_name(string)));
    assert(match(path.root_directory(), zix_path_root_directory(string)));
    assert(match(path.root_path(), zix_path_root_path(string)));
    assert(equal(path.relative_path(), zix_path_relative_path(string)));
    assert(match(path.parent_path(), zix_path_parent_path(string)));
    assert(equal(path.filename(), zix_path_filename(string)));
    assert(equal(path.stem(), zix_path_stem(string)));
    assert(equal(path.extension(), zix_path_extension(string)));

    // Queries
    assert(path.has_root_path() == zix_path_has_root_path(string));
    assert(path.has_root_name() == zix_path_has_root_name(string));
    assert(path.has_root_directory() == zix_path_has_root_directory(string));
    assert(path.has_relative_path() == zix_path_has_relative_path(string));
    assert(path.has_parent_path() == zix_path_has_parent_path(string));
    assert(path.has_filename() == zix_path_has_filename(string));
    assert(path.has_stem() == zix_path_has_stem(string));
    assert(path.has_extension() == zix_path_has_extension(string));
    assert(path.is_absolute() == zix_path_is_absolute(string));
    assert(path.is_relative() == zix_path_is_relative(string));

    // Generation

    assert(
      equal(Path{path}.make_preferred(), zix_path_preferred(nullptr, string)));

    assert(match(path.lexically_normal(),
                 zix_path_lexically_normal(nullptr, string)));
  }

  for (const auto& join : joins) {
    char* const joined = zix_path_join(nullptr, join.lhs, join.rhs);
    const Path  l      = join.lhs ? Path{join.lhs} : Path{};
    const Path  r      = join.rhs ? Path{join.rhs} : Path{};

    assert(equal(l / r, joined));
  }

  for (const auto& relatives : lexical_relatives) {
    assert(relatives.lhs);
    assert(relatives.rhs);

    const Path l = Path{relatives.lhs};
    const Path r = Path{relatives.rhs};

    assert(match(
      l.lexically_relative(r),
      zix_path_lexically_relative(nullptr, relatives.lhs, relatives.rhs)));

    assert(match(
      r.lexically_relative(l),
      zix_path_lexically_relative(nullptr, relatives.rhs, relatives.lhs)));
  }
}

} // namespace

int
main()
{
  run();
  return 0;
}
