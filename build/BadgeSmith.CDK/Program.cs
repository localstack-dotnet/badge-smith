using Amazon.CDK;
using BadgeSmith.CDK.Shared;

var app = new App();
_ = new ProductionStack(app, "BadgeSmithStack", new StackProps());
app.Synth();
