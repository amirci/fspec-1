require 'rake/clean'
require 'bundler/setup'
require 'albacore'
require 'albacore/tasks/versionizer'

Albacore::Tasks::Versionizer.new :versioning

CLEAN.include("output/*.dll", "output/*.exe")

def windows?
  RUBY_PLATFORM =~ /mingw/i 
end

desc 'restore all nugets as per the packages.config files'
nugets_restore :restore do |p|
  p.out = 'packages'
  p.exe = 'nuget.exe'
end

desc 'Perform full build'
build :build => [:versioning] do |b|
  b.sln = 'FSpec.sln'
end

task :test => ['output/fspec-runner.exe', 'output/FSpec.SelfTests.dll'] do
  executer = ""
  executer = "mono " unless windows?
  sh("#{executer}output/fspec-runner.exe output/FSpec.SelfTests.dll")
end

task :default => [:build, :test]
