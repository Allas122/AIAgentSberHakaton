namespace ChatNode.Application.Exceptions;

public class InfrastructureLayerException(string message) : Exception(message);

public class NotFoundException(string message) : InfrastructureLayerException(message);

public class PermissionDenied(string message) : InfrastructureLayerException(message);