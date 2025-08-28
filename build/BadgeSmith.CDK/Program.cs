using Amazon.CDK;
using BadgeSmith.CDK.Shared;
using static BadgeSmith.Constants;

var app = new App();
_ = new ProductionStack(app, ProductionStackId, new StackProps());
app.Synth();
